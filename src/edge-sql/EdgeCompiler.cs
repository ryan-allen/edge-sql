﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;

public class EdgeCompiler
{
    public Func<object, Task<object>> CompileFunc(IDictionary<string, object> parameters)
    {
        string command = ((string)parameters["source"]).TrimStart();
        string connectionString = Environment.GetEnvironmentVariable("EDGE_SQL_CONNECTION_STRING");
        object tmp;
        if (parameters.TryGetValue("connectionString", out tmp))
        {
            connectionString = (string)tmp;
        }

        return async (queryParameters) =>
        {
            return await this.ExecuteQuery(connectionString, command, (IDictionary<string, object>)queryParameters);
        };
    }

    void AddParamaters(SqlCommand command, IDictionary<string, object> parameters)
    {
        if (parameters != null)
        {
            foreach (KeyValuePair<string, object> parameter in parameters)
            {
                command.Parameters.AddWithValue(parameter.Key, parameter.Value);
            }
        }
    }

    async Task<object> ExecuteQuery(string connectionString, string commandString, IDictionary<string, object> parameters)
    {
        List<object> rows = new List<object>();

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            using (SqlCommand command = new SqlCommand(commandString, connection))
            {
                this.AddParamaters(command, parameters);
                await connection.OpenAsync();
                using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.CloseConnection))
                {
                    object[] fieldNames = new object[reader.FieldCount];
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        fieldNames[i] = reader.GetName(i);
                    }
                    rows.Add(fieldNames);

                    IDataRecord record = (IDataRecord)reader;
                    while (await reader.ReadAsync())
                    {
                        object[] resultRecord = new object[record.FieldCount];
                        record.GetValues(resultRecord);
                        for (int i = 0; i < record.FieldCount; i++)
                        {      
                            Type type = record.GetFieldType(i);
                            if (resultRecord[i] is System.DBNull)
                            {
                                resultRecord[i] = null;
                            }
                            else if (type == typeof(byte[]) || type == typeof(char[]))
                            {
                                resultRecord[i] = Convert.ToBase64String((byte[])resultRecord[i]);
                            }
                            else if (type == typeof(Guid) || type == typeof(DateTime))
                            {
                                resultRecord[i] = resultRecord[i].ToString();
                            }
                            else if (type == typeof(IDataReader))
                            {
                                resultRecord[i] = "<IDataReader>";
                            }
                        }

                        rows.Add(resultRecord);
                    }
                }
            }
        }

        return rows;
    }

    async Task<object> ExecuteNonQuery(string connectionString, string commandString, IDictionary<string, object> parameters)
    {
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            using (SqlCommand command = new SqlCommand(commandString, connection))
            {
                this.AddParamaters(command, parameters);
                await connection.OpenAsync();
                return await command.ExecuteNonQueryAsync();
            }
        }
    }
}
