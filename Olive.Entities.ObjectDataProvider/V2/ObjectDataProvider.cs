﻿using Olive.Entities.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

namespace Olive.Entities.ObjectDataProvider.V2
{
    public class ObjectDataProvider<TConnection, TDataParameter> : DataProvider<TConnection, TDataParameter>
         where TConnection : DbConnection, new()
         where TDataParameter : IDbDataParameter, new()
    {
        readonly Type entityType;

        string Fields;
        string TablesTemplate;
        Dictionary<string, string> ColumnMapping = new Dictionary<string, string>();

        public SqlCommandGenerator SqlCommandGenerator { get; }

        public DataProviderMetaData MetaData {get;}

        public override Type EntityType => entityType;

        public string DeleteCommand { get; }

        public string UpdateCommand { get; }

        public string InsertCommand { get; }

        public ObjectDataProvider(Type type, ICache cache, SqlCommandGenerator sqlCommandGenerator)
            : base(cache)
        {
            entityType = type;
            SqlCommandGenerator = sqlCommandGenerator;

            MetaData = DataProviderMetaDataGenerator.Generate(type);

            DeleteCommand = SqlCommandGenerator.GenerateDeleteCommand(MetaData);
            UpdateCommand = SqlCommandGenerator.GenerateUpdateCommand(MetaData);
            InsertCommand = SqlCommandGenerator.GenerateInsertCommand(MetaData);

            PrepareTableTemplate();
            PrepareFields();
            PrepareColumnMappingDictonary();
        }

        public override string MapColumn(string propertyName) => ColumnMapping[propertyName];
        //{
        //    if (propertyName == "ID" || MetaData.Properties.Any(p => p.IsPrimaryKey))
        //        return GetSqlCommandColumn(MetaData, MetaData.Properties.First(p => p.IsPrimaryKey));

        //    foreach (var prop in MetaData.Properties)
        //        if (prop.Name == propertyName)
        //            return GetSqlCommandColumn(MetaData, prop);

        //    foreach (var parent in MetaData.BaseClassesInOrder)
        //        foreach (var prop in parent.Properties)
        //            if (prop.Name == propertyName)
        //                return GetSqlCommandColumn(parent, prop);

        //    throw new ArgumentOutOfRangeException(nameof(propertyName));
        //}

        protected override string SafeId(string objectName)
        {
            throw new NotImplementedException("Should be moved to data access class.");
        }

        public override Task Delete(IEntity record)
        {
            if (MetaData.BaseClassesInOrder.Any())
                return MetaData.BaseClassesInOrder.First().GetProvider<TConnection, TDataParameter>(Cache, SqlCommandGenerator).Delete(record);

            return ExecuteNonQuery(DeleteCommand, CommandType.Text, Access.CreateParameter("Id", record.GetId()));
        }

        public override Task<IEnumerable<string>> ReadManyToManyRelation(IEntity instance, string property)
        {
            throw new NotImplementedException();
        }

        public override async Task Save(IEntity record)
        {
            if (record.IsNew)
                await Insert(record);
            else
                await Update(record);
        }

        public override string GetFields() => Fields;

        public override string GetTables(string prefix = null) => TablesTemplate.FormatWith(prefix);

        public override IEntity Parse(IDataReader reader)
        {
            for (int index = MetaData.DrivedClasses.Length - 1; index > -1; index--)
            {
                var current = MetaData.DrivedClasses[index];

                if (reader[GetSqlCommandColumnAlias(current, current.IdColumnName)] != DBNull.Value)
                    return current.GetProvider<TConnection, TDataParameter>(Cache, SqlCommandGenerator).Parse(reader);
            }

            if(MetaData.Type.IsAbstract)
                throw new Exception($"The record with ID of '{reader[GetSqlCommandColumnAlias(MetaData, MetaData.IdColumnName)]}' exists only in the abstract database table of '{MetaData.TableName}' and no concrete table. The data needs cleaning-up.");

            var result = (IEntity) Activator.CreateInstance(EntityType);

            FillData(reader, result);

            foreach (var parent in MetaData.BaseClassesInOrder)
                parent.GetProvider<TConnection, TDataParameter>(Cache, SqlCommandGenerator).FillData(reader, result);

            Entity.Services.SetSaved(result, saved: true);

            return result;
        }

        public override string GenerateSelectCommand(IDatabaseQuery iquery, string fields) =>
            SqlCommandGenerator.GenerateSelectCommand(iquery, GetTables(iquery.AliasPrefix), fields);

        public override string GenerateWhere(DatabaseQuery query) =>
            SqlCommandGenerator.GenerateWhere(query);

        async Task Update(IEntity record)
        {
            async Task saveAll()
            {
                foreach (var parent in MetaData.BaseClassesInOrder)
                    await parent.GetProvider<TConnection, TDataParameter>(Cache, SqlCommandGenerator).Update(record);

                if ((await ExecuteScalar(UpdateCommand, CommandType.Text, CreateParameters(record))).ToStringOrEmpty().IsEmpty())
                {
                    Cache.Remove(record);
                    throw new ConcurrencyException($"Failed to update the '{MetaData.TableName}' table. There is no row with the ID of {record.GetId()}.");
                }
            }

            if (Database.AnyOpenTransaction()) await saveAll();
            else using (var scope = Database.CreateTransactionScope()) { await saveAll(); scope.Complete(); }
        }

        private IDataParameter[] CreateParameters(IEntity record)
        {
            var result = new List<IDataParameter>();

            foreach (var prop in MetaData.Properties)
                result.Add(Access.CreateParameter(prop.ParameterName, prop.GetValue(record) ?? DBNull.Value));

            return result.ToArray();
        }

        async Task Insert(IEntity record)
        {
            async Task saveAll()
            {
                foreach (var parent in MetaData.BaseClassesInOrder)
                    await parent.GetProvider<TConnection, TDataParameter>(Cache, SqlCommandGenerator).Insert(record);

                var result = await ExecuteScalar(InsertCommand, CommandType.Text, CreateParameters(record));

                if (MetaData.HasAutoNumber)
                    MetaData.AutoNumberProperty.PropertyInfo.SetValue(record, result);
            }

            if (Database.AnyOpenTransaction()) await saveAll();
            else using (var scope = Database.CreateTransactionScope()) { await saveAll(); scope.Complete(); }
        }

        void FillData(IDataReader reader, IEntity entity)
        {
            foreach (var property in MetaData.UserDefienedAndIdProperties)
            {
                var value = reader[GetSqlCommandColumnAlias(MetaData, property)];

                if(value != DBNull.Value)
                    property.SetValue(entity, value);
            }
        }

        string GetSqlCommandColumn(DataProviderMetaData medaData, PropertyData property) =>
            GetSqlCommandColumn(medaData, property.Name);

        string GetSqlCommandColumn(DataProviderMetaData medaData, string propertyName) =>
            $"{medaData.TableAlias}.[{propertyName}]";

        string GetSqlCommandColumnAlias(DataProviderMetaData medaData, PropertyData property) => 
            GetSqlCommandColumnAlias(medaData, property.Name);

        string GetSqlCommandColumnAlias(DataProviderMetaData medaData, string propertyName) => 
            $"{medaData.TableName}_{propertyName}";

        void PrepareTableTemplate()
        {
            TablesTemplate = "";
            DataProviderMetaData temp = null;

            void addTable(DataProviderMetaData medaData)
            {
                TablesTemplate += " LEFT OUTER JOIN ".OnlyWhen(temp != null) +
                    $"{medaData.Schema.WithSuffix(".")}{medaData.TableName} AS {{0}}{medaData.TableAlias} " +
                    $"ON {{0}}{medaData.TableAlias}.{medaData.IdColumnName} = {{0}}{temp?.TableAlias}.{temp?.IdColumnName}".OnlyWhen(temp != null);

                temp = medaData;
            }

            foreach (var parent in MetaData.BaseClassesInOrder)
                addTable(parent);

            addTable(MetaData);

            foreach (var drived in MetaData.DrivedClasses)
                addTable(drived);
        }

        void PrepareFields()
        {
            Fields = "";

            foreach (var parent in MetaData.BaseClassesInOrder)
                foreach (var prop in parent.UserDefienedAndIdProperties)
                    Fields += $"{GetSqlCommandColumn(parent, prop)} as {GetSqlCommandColumnAlias(parent, prop)}, ";

            foreach (var prop in MetaData.UserDefienedAndIdProperties)
                Fields += $"{GetSqlCommandColumn(MetaData, prop)} as {GetSqlCommandColumnAlias(MetaData, prop)}, ";

            foreach (var parent in MetaData.DrivedClasses)
                foreach (var prop in parent.UserDefienedAndIdProperties)
                    Fields += $"{GetSqlCommandColumn(parent, prop)} as {GetSqlCommandColumnAlias(parent, prop)}, ";

            Fields = Fields.TrimEnd(2);
        }

        void PrepareColumnMappingDictonary()
        {
            ColumnMapping.Add(PropertyData.DEFAULT_ID_COLUMN, GetSqlCommandColumn(MetaData, MetaData.IdColumnName));

            if(MetaData.IsSoftDeleteEnabled)
                ColumnMapping.Add(
                    PropertyData.IS_MARKED_SOFT_DELETED, 
                    GetSqlCommandColumn(MetaData, MetaData.Properties.First(p => p.IsDeleted)));

            foreach (var prop in MetaData.UserDefienedProperties)
                ColumnMapping.Add(prop.Name, GetSqlCommandColumn(MetaData, prop));

            foreach (var parent in MetaData.BaseClassesInOrder)
                foreach (var prop in parent.UserDefienedProperties)
                    ColumnMapping.Add(prop.Name, GetSqlCommandColumn(MetaData, prop));
        }
    }
}
