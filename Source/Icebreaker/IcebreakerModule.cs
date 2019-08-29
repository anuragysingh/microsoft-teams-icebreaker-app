// <copyright file="IcebreakerModule.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Icebreaker
{
    using Autofac;
    using Icebreaker.Helpers;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.Azure;

    /// <summary>
    /// Autofac Module
    /// </summary>
    public class IcebreakerModule : Module
    {
        private const string StorageName = "Cosmos";
        private static string databaseKind = CloudConfigurationManager.GetSetting("DatabaseKind");

        /// <inheritdoc/>
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.Register(c =>
            {
                return new TelemetryClient(new TelemetryConfiguration(CloudConfigurationManager.GetSetting("APPINSIGHTS_INSTRUMENTATIONKEY")));
            }).SingleInstance();

            builder.RegisterType<IcebreakerBot>()
                .SingleInstance();

            if (databaseKind.Equals(StorageName) || databaseKind == string.Empty)
            {
                builder.RegisterType<CosmosDataProvider>().AsImplementedInterfaces();
            }
            else
            {
                builder.RegisterType<TableStorageDataProvider>().AsImplementedInterfaces();
            }
        }
    }
}