using System;
using System.Data;
using Npgsql;
using System.Threading.Tasks;

namespace QuantApp.Services
{
    public class DatabaseService
    {
        private readonly string connectionString =
            "Host=localhost;Database=stock_db;Username=postgres;Password=Apple132!";

        public async Task<DataTable> GetStockDataAsync()
        {
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            // Main query
            string query = @"
        SELECT * FROM ""PER_and_dividend""
        WHERE ""fs_nm"" = '재무제표'
        AND ""DPR"" > 0
        AND ""bsns_year"" = '2024'
        ORDER BY ""PER""";

            using var cmd = new NpgsqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var dt = new DataTable();
            dt.Load(reader);

            // Add custom columns for checking
            dt.Columns.Add("IsChecked", typeof(bool));
            dt.Columns.Add("LastChecked", typeof(DateTime));
            dt.Columns.Add("LastCheckedDisplay", typeof(string));

            // Get check history
            var checkHistory = await GetCheckHistoryAsync();

            // Update rows with check history
            foreach (DataRow row in dt.Rows)
            {
                string stockCode = row["stock_code"].ToString();

                if (checkHistory.ContainsKey(stockCode) && checkHistory[stockCode].HasValue)
                {
                    row["IsChecked"] = true;
                    row["LastChecked"] = checkHistory[stockCode].Value;
                    row["LastCheckedDisplay"] = $"Last checked: {checkHistory[stockCode].Value:yy/MM/dd}";
                }
                else
                {
                    row["IsChecked"] = false;
                    row["LastCheckedDisplay"] = "Not checked";
                }
            }

            return dt;
        }

        public async Task<DataTable> GetMarketCapAsync()
        {
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            string query = "SELECT * FROM market_cap";

            using var cmd = new NpgsqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var dt = new DataTable();
            dt.Load(reader);

            return dt;
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<Dictionary<string, DateTime?>> GetCheckHistoryAsync()
        {
            var checkHistory = new Dictionary<string, DateTime?>();

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            // First, check if the table exists
            string checkTableQuery = @"
            SELECT EXISTS (
                SELECT FROM information_schema.tables 
                WHERE table_name = 'company_checks'
            )";

            using var checkCmd = new NpgsqlCommand(checkTableQuery, conn);
            bool tableExists = (bool)await checkCmd.ExecuteScalarAsync();

            // If table doesn't exist, create it
            if (!tableExists)
            {
                string createTableQuery = @"
                CREATE TABLE company_checks (
                    stock_code VARCHAR(10) PRIMARY KEY,
                    last_checked TIMESTAMP,
                    notes TEXT
                )";

                using var createCmd = new NpgsqlCommand(createTableQuery, conn);
                await createCmd.ExecuteNonQueryAsync();

                // Return empty dictionary since no data yet
                return checkHistory;
            }

            // Get the check history
            string query = "SELECT stock_code, last_checked FROM company_checks";
            using var cmd = new NpgsqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var stockCode = reader.GetString(0);
                var lastChecked = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
                checkHistory[stockCode] = lastChecked;
            }

            return checkHistory;
        }

        public async Task MarkAsCheckedAsync(string stockCode)
        {
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            string query = @"
            INSERT INTO company_checks (stock_code, last_checked) 
            VALUES (@stockCode, @checkedTime)
            ON CONFLICT (stock_code) 
            DO UPDATE SET last_checked = @checkedTime";

            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@stockCode", stockCode);
            cmd.Parameters.AddWithValue("@checkedTime", DateTime.Now);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UnmarkAsCheckedAsync(string stockCode)
        {
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            string query = "DELETE FROM company_checks WHERE stock_code = @stockCode";

            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@stockCode", stockCode);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}