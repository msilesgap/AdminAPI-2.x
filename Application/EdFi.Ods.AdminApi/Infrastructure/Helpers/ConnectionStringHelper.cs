// SPDX-License-Identifier: Apache-2.0
// Licensed to the Ed-Fi Alliance under one or more agreements.
// The Ed-Fi Alliance licenses this file to you under the Apache License, Version 2.0.
// See the LICENSE and NOTICES files in the project root for more information.

using Microsoft.Data.SqlClient;
using Npgsql;

namespace EdFi.Ods.AdminApi.Infrastructure.Helpers;

public static class ConnectionStringHelper
{
    public static bool ValidateConnectionString(string databaseEngine, string? connectionString)
    {
        bool result = true;
        if (databaseEngine == "SqlServer")
        {
            try
            {
                SqlConnectionStringBuilder sqlConnectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            }
            catch (ArgumentException)
            {
                result = false;
            }
        }
        else if (databaseEngine == "PostgreSQL")
        {
            try
            {
                NpgsqlConnectionStringBuilder npgsqlConnectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            }
            catch (ArgumentException)
            {
                result = false;
            }
        }
        return result;
    }
}
