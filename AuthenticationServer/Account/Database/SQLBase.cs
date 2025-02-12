﻿using System.Configuration;
using System.Runtime.InteropServices;
using MySql.Data.MySqlClient;

namespace AuthServer.Account.Database
{
    public class SQLBase
    {
        public MySqlConnection con { get; private set; }

        public SQLBase()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["DevLocal"].ConnectionString;

            //Set connection property from connection string and open connection
            con = new MySqlConnection(connectionString);
            con.Open();
        }

        public void CloseConnection()
        {
            con.Close();
        }
    }
}
