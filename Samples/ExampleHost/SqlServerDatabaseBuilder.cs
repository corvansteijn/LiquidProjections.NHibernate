using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Reflection;
using FluentNHibernate.Automapping;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using LiquidProjections.ExampleHost.Projections;
using NHibernate;
using NHibernate.Tool.hbm2ddl;

namespace LiquidProjections.ExampleHost
{
    internal sealed class SqlServerDatabaseBuilder
    {
        public SqlServerDatabase Build()
        {
            var connectionStringSettings = new ConnectionStringSettings(
                "projections", "Data Source=192.168.56.1;Initial Catalog=ExampleHost; User ID=test;Password=test;Application Name=ExampleHost", "sqlite");

            var connection = new SqlConnection(connectionStringSettings.ConnectionString);
            connection.Open();

            ISessionFactory sessionFactory = Fluently.Configure()
                .Database(MsSqlConfiguration.MsSql2012.ConnectionString(connectionStringSettings.ConnectionString))
                .Mappings(configuration => configuration.AutoMappings.Add(AutoMap
                    .AssemblyOf<DocumentCountProjection>(new FluentConfiguration())
                    .UseOverridesFromAssembly(Assembly.GetExecutingAssembly())
                    ))
                .ExposeConfiguration(configuration => new SchemaExport(configuration)
                    .Execute(useStdOut: true, execute: true, justDrop: false))
                .BuildSessionFactory();

            return new SqlServerDatabase(connection, sessionFactory);
        }
    }

    internal sealed class SqlServerDatabase : IDisposable
    {
        private readonly SqlConnection connection;

        public SqlServerDatabase(SqlConnection connection, ISessionFactory sessionFactory)
        {
            this.connection = connection;
            SessionFactory = sessionFactory;
        }

        public ISessionFactory SessionFactory { get; }

        public void Dispose()
        {
            connection?.Dispose();
        }
    }
}