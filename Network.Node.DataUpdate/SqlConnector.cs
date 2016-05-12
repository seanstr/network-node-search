using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace Network.Node.DataUpdate
{
    public class SqlConnector
    {
        private SqlConnection cn;

        internal void logger(string message)
        {
            string logFile = @"e:\IIS-Logfiles\log.txt";
            System.IO.File.AppendAllText(logFile, DateTimeOffset.Now.ToString("u") + "  " + message + Environment.NewLine);
        }

        public SqlConnector()
        {
            this.cn = new SqlConnection(ConfigurationManager.ConnectionStrings["clliEntities"].ConnectionString);
        }

        public void LoadTable(DataTable table)
        {
            cn.Open();
            using (var cm = new SqlCommand(GetCommandText(table.TableName), cn))
            {
                table.Load(cm.ExecuteReader());
            }
            cn.Close();
        }

        public void LoadTable(DataTable table, string filter)
        {
            cn.Open();
            using (var cm = new SqlCommand(GetCommandText(table.TableName, filter), cn))
            {
                table.Load(cm.ExecuteReader());
            }
            cn.Close();
        }

        public void ClearTable(DataTable table)
        {
            try
            {
                table.Clear();
            }
            catch (Exception e)
            {
                logger(string.Format("Exception caught = {0}", e));
            }
        }
        
        public int UpdateTable(DataTable table)
        {
            int affectedRows = 0;

            cn.Open();
            using (var da = new SqlDataAdapter())
            {
                da.SelectCommand = new SqlCommand(GetCommandText(table.TableName), cn);

                using (var cb = new SqlCommandBuilder(da))
                {
                    da.DeleteCommand = cb.GetDeleteCommand();
                    da.InsertCommand = cb.GetInsertCommand();

                    affectedRows = da.Update(table);
                }
            }
            cn.Close();

            return affectedRows;
        }

        internal string GetCommandText(string tableName)
        {
            return string.Format("SELECT * FROM {0}", tableName);
        }

        internal string GetCommandText(string tableName, string filter)
        {
            /* checking */
            return string.Format("SELECT * FROM {0} where {1}", tableName, filter);
        }
    }
}
