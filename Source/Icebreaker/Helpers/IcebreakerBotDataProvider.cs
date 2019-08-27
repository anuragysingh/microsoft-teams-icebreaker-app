//----------------------------------------------------------------------------------------------
// <copyright file="IcebreakerBotDataProvider.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

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
    public class IcebreakerBotDataProvider
    {
        // Request the minimum throughput by default
        private const int DefaultRequestThroughput = 400;
        private const string TeamsPartitionKey = "TeamsInfo";
        private const string UsersPartitionKey = "UsersInfo";

        private readonly TelemetryClient telemetryClient;
        private readonly Lazy<Task> initializeTask;
        private CloudTable teamsCollectionCloudTable;
        private CloudTable usersCollectionCloudTable;

        /// <summary>
        /// Initializes a new instance of the <see cref="IcebreakerBotDataProvider"/> class.
        /// </summary>
        /// <param name="telemetryClient">The telemetry client to use</param>
        public IcebreakerBotDataProvider(TelemetryClient telemetryClient)
        {
            this.telemetryClient = telemetryClient;
            this.initializeTask = new Lazy<Task>(() => this.InitializeAsync());
        }

        /// <summary>
        /// Updates team installation status in store. If the bot is installed, the info is saved, otherwise info for the team is deleted.
        /// </summary>
        /// <param name="team">The team installation info</param>
        /// <param name="installed">Value that indicates if bot is installed</param>
        /// <returns>Tracking task</returns>
        public async Task UpdateTeamInstallStatusAsync(TeamInstallInfo team, bool installed)
        {
            await this.EnsureInitializedAsync();

            if (installed)
            {
                team.PartitionKey = TeamsPartitionKey;

                TableOperation addOrUpdateOperation = TableOperation.InsertOrReplace(team);
                await this.teamsCollectionCloudTable.ExecuteAsync(addOrUpdateOperation);
            }
            else
            {
                TableOperation deleteOperation = TableOperation.Delete(team);
                await this.teamsCollectionCloudTable.ExecuteAsync(deleteOperation);
            }
        }

        /// <summary>
        /// Get the list of teams to which the app was installed.
        /// </summary>
        /// <returns>List of installed teams</returns>
        public async Task<IList<TeamInstallInfo>> GetInstalledTeamsAsync()
        {
            await this.EnsureInitializedAsync();

            var installedTeams = new List<TeamInstallInfo>();

            try
            {
                TableQuery<TeamInstallInfo> projectionQuery = new TableQuery<TeamInstallInfo>();
                TableContinuationToken token = null;

                do
                {
                    TableQuerySegment<TeamInstallInfo> seg = await this.teamsCollectionCloudTable.ExecuteQuerySegmentedAsync(projectionQuery, token);
                    token = seg.ContinuationToken;
                    installedTeams.AddRange(seg.Results);
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

        /// <summary>
        /// Returns the team that the bot has been installed to
        /// </summary>
        /// <param name="teamId">The team id</param>
        /// <returns>Team that the bot is installed to</returns>
        public async Task<TeamInstallInfo> GetInstalledTeamAsync(string teamId)
        {
            await this.EnsureInitializedAsync();

            // Get team install info
            try
            {
                var searchOperation = TableOperation.Retrieve<TeamInstallInfo>(TeamsPartitionKey, teamId);
                TableResult searchResult = await this.teamsCollectionCloudTable.ExecuteAsync(searchOperation);
                return (TeamInstallInfo)searchResult.Result;
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackException(ex.InnerException);
                return null;
            }
        }

        /// <summary>
        /// Get the stored information about the given user
        /// </summary>
        /// <param name="userId">User id</param>
        /// <returns>User information</returns>
        public async Task<UserInfo> GetUserInfoAsync(string userId)
        {
            await this.EnsureInitializedAsync();

            try
            {
                var searchOperation = TableOperation.Retrieve<UserInfo>(UsersPartitionKey, userId);
                TableResult searchResult = await this.usersCollectionCloudTable.ExecuteAsync(searchOperation);
                return (UserInfo)searchResult.Result;
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackException(ex.InnerException);
                return null;
            }
        }

        /// <summary>
        /// Set the user info for the given user
        /// </summary>
        /// <param name="tenantId">Tenant id</param>
        /// <param name="userId">User id</param>
        /// <param name="optedIn">User opt-in status</param>
        /// <param name="serviceUrl">User service URL</param>
        /// <returns>Tracking task</returns>
        public async Task SetUserInfoAsync(string tenantId, string userId, bool optedIn, string serviceUrl)
        {
            await this.EnsureInitializedAsync();

            var userInfo = new UserInfo
            {
                PartitionKey = UsersPartitionKey,
                TenantId = tenantId,
                UserId = userId,
                OptedIn = optedIn,
                ServiceUrl = serviceUrl
            };

            TableOperation addOrUpdateOperation = TableOperation.InsertOrReplace(userInfo);
            await this.usersCollectionCloudTable.ExecuteAsync(addOrUpdateOperation);
        }

        /// <summary>
        /// Initializes the database connection.
        /// </summary>
        /// <returns>Tracking task</returns>
        private async Task InitializeAsync()
        {
            this.telemetryClient.TrackTrace("Initializing data store");
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
            CloudTableClient cloudTableClient = storageAccount.CreateCloudTableClient();
            this.teamsCollectionCloudTable = cloudTableClient.GetTableReference(StorageInfo.TeamsTableName);
            this.usersCollectionCloudTable = cloudTableClient.GetTableReference(StorageInfo.UsersTableName);

            // Create the database if needed
            try
            {
                await this.teamsCollectionCloudTable.CreateIfNotExistsAsync();
                await this.usersCollectionCloudTable.CreateIfNotExistsAsync();
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