// <copyright file="StorageInfo.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
namespace Icebreaker.Helpers
{
    /// <summary>
    /// References to storage table.
    /// </summary>
    public class StorageInfo
    {
        /// <summary>
        /// Table name where teams installed details will be saved
        /// </summary>
        public const string TeamsTableName = "TeamsInstalled";

        /// <summary>
        /// Table name users status will be saved
        /// </summary>
        public const string UsersTableName = "UsersOptInStatus";
    }
}