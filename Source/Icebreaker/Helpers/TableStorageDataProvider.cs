// <copyright file="TableStorageDataProvider.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
namespace Icebreaker.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights;
    using Microsoft.Azure;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;

    /// <summary>
    /// Data provider routines
    /// </summary>
    public class TableStorageDataProvider : IBotDataProvider
    {
        // Request the minimum throughput by default
        private const int DefaultRequestThroughput = 400;
        private const string TeamsPartitionKey = "TeamsInfo";
        private const string UsersPartitionKey = "UsersInfo";

        private readonly TelemetryClient telemetryClient;
        private readonly Lazy<Task> initializeTask;
        private CloudTable teamsCloudTable;
        private CloudTable usersCloudTable;

        /// <summary>
        /// Initializes a new instance of the <see cref="TableStorageDataProvider"/> class.
        /// </summary>
        /// <param name="telemetryClient">The telemetry client to use</param>
        public TableStorageDataProvider(TelemetryClient telemetryClient)
        {
            this.telemetryClient = telemetryClient;
            this.initializeTask = new Lazy<Task>(() => this.InitializeAsync());
        }

        /// <inheritdoc/>
        public async Task UpdateTeamInstallStatusAsync(TeamInstallInfo team, bool installed)
        {
            await this.EnsureInitializedAsync();

            var teamEntity = new TeamInstallEntity
            {
                PartitionKey = TeamsPartitionKey,
                InstallerName = team.InstallerName,
                ServiceUrl = team.ServiceUrl,
                TeamId = team.TeamId,
                TenantId = team.TenantId
            };

            if (installed)
            {
                TableOperation addOrUpdateOperation = TableOperation.InsertOrReplace(teamEntity);
                await this.teamsCloudTable.ExecuteAsync(addOrUpdateOperation);
            }
            else
            {
                var entity = new DynamicTableEntity(TeamsPartitionKey, teamEntity.TeamId);
                entity.ETag = "*";

                await this.teamsCloudTable.ExecuteAsync(TableOperation.Delete(entity));
            }
        }

        /// <inheritdoc/>
        public async Task<IList<TeamInstallInfo>> GetInstalledTeamsAsync()
        {
            await this.EnsureInitializedAsync();

            var installedTeams = new List<TeamInstallInfo>();

            try
            {
                TableQuery<TeamInstallEntity> projectionQuery = new TableQuery<TeamInstallEntity>();
                TableContinuationToken token = null;

                do
                {
                    TableQuerySegment<TeamInstallEntity> seg = await this.teamsCloudTable.ExecuteQuerySegmentedAsync(projectionQuery, token);
                    token = seg.ContinuationToken;
                    foreach (var teamInfo in seg.Results)
                    {
                        installedTeams.Add(new TeamInstallInfo
                        {
                            InstallerName = teamInfo.InstallerName,
                            ServiceUrl = teamInfo.ServiceUrl,
                            TeamId = teamInfo.TeamId,
                            TenantId = teamInfo.TenantId
                        });
                    }
                }
                while (token != null);

                return installedTeams;
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackException(ex.InnerException);
            }

            return installedTeams;
        }

        /// <inheritdoc/>
        public async Task<TeamInstallInfo> GetInstalledTeamAsync(string teamId)
        {
            await this.EnsureInitializedAsync();

            // Get team install info
            try
            {
                var searchOperation = TableOperation.Retrieve<TeamInstallEntity>(TeamsPartitionKey, teamId);
                TableResult searchResult = await this.teamsCloudTable.ExecuteAsync(searchOperation);
                return (TeamInstallInfo)searchResult.Result;
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackException(ex.InnerException);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<UserInfo> GetUserInfoAsync(string userId)
        {
            await this.EnsureInitializedAsync();

            try
            {
                var searchOperation = TableOperation.Retrieve<UserEntity>(UsersPartitionKey, userId);
                TableResult searchResult = await this.usersCloudTable.ExecuteAsync(searchOperation);
                return (UserInfo)searchResult.Result;
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackException(ex.InnerException);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task SetUserInfoAsync(string tenantId, string userId, bool optedIn, string serviceUrl)
        {
            await this.EnsureInitializedAsync();

            var userInfo = new UserEntity
            {
                PartitionKey = UsersPartitionKey,
                TenantId = tenantId,
                UserId = userId,
                OptedIn = optedIn,
                ServiceUrl = serviceUrl
            };

            TableOperation addOrUpdateOperation = TableOperation.InsertOrReplace(userInfo);
            await this.usersCloudTable.ExecuteAsync(addOrUpdateOperation);
        }

        /// <summary>
        /// Initializes the database connection.
        /// </summary>
        /// <returns>Tracking task</returns>
        private async Task InitializeAsync()
        {
            this.telemetryClient.TrackTrace("Initializing data store");
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("TableStorageConnectionString"));
            CloudTableClient cloudTableClient = storageAccount.CreateCloudTableClient();
            this.teamsCloudTable = cloudTableClient.GetTableReference(StorageInfo.TeamsTableName);
            this.usersCloudTable = cloudTableClient.GetTableReference(StorageInfo.UsersTableName);

            // Create the database if needed
            try
            {
                await this.teamsCloudTable.CreateIfNotExistsAsync();
                await this.usersCloudTable.CreateIfNotExistsAsync();
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Unable to create {StorageInfo.TeamsTableName} and {StorageInfo.UsersTableName} tables due to: {ex}");
                throw;
            }

            this.telemetryClient.TrackTrace("Data store initialized");
        }

        private async Task EnsureInitializedAsync()
        {
            await this.initializeTask.Value;
        }
    }
}