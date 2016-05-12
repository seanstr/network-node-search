using System;
using System.Collections.Specialized;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Web;
using System.Web.SessionState;

namespace Network.Node.Session
{
    public class Session : SessionStateStoreProviderBase
    {
        private string _connectionString;
        private int _rqOrigStreamLen;
        private const int ITEM_SHORT_LENGTH = 7000;

        public override void Initialize(string name, NameValueCollection config)
        {
            _connectionString = config["connectionString"];
            base.Initialize(name, config);
        }

        public override void Dispose()
        {
        }

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            return false;
        }

        public override void InitializeRequest(HttpContext context)
        {
            _rqOrigStreamLen = 0;
        }

        public override SessionStateStoreData GetItem(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId,
            out SessionStateActions actions)
        {
            return DoGet(context, id, false, out locked, out lockAge, out lockId, out actions);
        }

        public override SessionStateStoreData GetItemExclusive(HttpContext context, string id, out bool locked, out TimeSpan lockAge,
            out object lockId, out SessionStateActions actions)
        {
            return DoGet(context, id, true, out locked, out lockAge, out lockId, out actions);
        }

        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            SqlStateConnection conn = null;
            int lockCookie = (int)lockId;

            try
            {
                conn = GetConnection();
                SqlCommand cmd = conn.TempReleaseExclusive;
                cmd.Parameters[0].Value = id;
                cmd.Parameters[1].Value = lockCookie;
                cmd.ExecuteNonQuery();

            }
            finally
            {
                DisposeOrReuseConnection(ref conn);
            }
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem)
        {
            byte[] buf;
            int length;
            SqlCommand cmd;
            SqlStateConnection conn = null;
            int lockCookie;

            try
            {
                try
                {
                    SerializeStoreData(item, ITEM_SHORT_LENGTH, out buf, out length);
                }
                catch
                {
                    if (!newItem)
                    {
                        ReleaseItemExclusive(context, id, lockId);
                    }
                    throw;
                }

                // Save it to the store

                if (lockId == null)
                {
                    lockCookie = 0;
                }
                else
                {
                    lockCookie = (int)lockId;
                }

                conn = GetConnection();

                if (!newItem)
                {
                    if (length <= ITEM_SHORT_LENGTH)
                    {
                        if (_rqOrigStreamLen <= ITEM_SHORT_LENGTH)
                        {
                            cmd = conn.TempUpdateShort;
                        }
                        else
                        {
                            cmd = conn.TempUpdateShortNullLong;
                        }
                    }
                    else
                    {
                        if (_rqOrigStreamLen <= ITEM_SHORT_LENGTH)
                        {
                            cmd = conn.TempUpdateLongNullShort;
                        }
                        else
                        {
                            cmd = conn.TempUpdateLong;
                        }
                    }

                }
                else
                {
                    if (length <= ITEM_SHORT_LENGTH)
                    {
                        cmd = conn.TempInsertShort;
                    }
                    else
                    {
                        cmd = conn.TempInsertLong;
                    }
                }

                cmd.Parameters[0].Value = id;
                cmd.Parameters[1].Size = length;
                cmd.Parameters[1].Value = buf;
                cmd.Parameters[2].Value = item.Timeout;
                if (!newItem)
                {
                    cmd.Parameters[3].Value = lockCookie;
                }
                SqlExecuteNonQuery(cmd, newItem);
            }
            finally
            {
                DisposeOrReuseConnection(ref conn);
            }
        }


        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            SqlStateConnection conn = null;
            int lockCookie = (int)lockId;

            try
            {
                conn = GetConnection();
                SqlCommand cmd = conn.TempRemove;
                cmd.Parameters[0].Value = id;
                cmd.Parameters[1].Value = lockCookie;
                cmd.ExecuteNonQuery();
            }
            finally
            {
                DisposeOrReuseConnection(ref conn);
            }
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            SqlStateConnection conn = null;

            try
            {
                conn = GetConnection();
                SqlCommand cmd = conn.TempResetTimeout;
                cmd.Parameters[0].Value = id;
                cmd.ExecuteNonQuery();
            }
            finally
            {
                DisposeOrReuseConnection(ref conn);
            }
        }

        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return new SessionStateStoreData(new SessionStateItemCollection(), SessionStateUtility.GetSessionStaticObjects(context), timeout);
        }

        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            SqlStateConnection conn = null;
            byte[] buf;
            int length;

            try
            {
                // Store an empty data
                SerializeStoreData(CreateNewStoreData(context, timeout),
                                ITEM_SHORT_LENGTH, out buf, out length);

                conn = GetConnection();

                SqlCommand cmd = conn.TempInsertUninitializedItem;
                cmd.Parameters[0].Value = id;
                cmd.Parameters[1].Size = length;
                cmd.Parameters[1].Value = buf;
                cmd.Parameters[2].Value = timeout;
                SqlExecuteNonQuery(cmd, true);
            }
            finally
            {
                DisposeOrReuseConnection(ref conn);
            }
        }

        public override void EndRequest(HttpContext context)
        {
        }


        SessionStateStoreData DoGet(HttpContext context, String id, bool getExclusive,
                                        out bool locked,
                                        out TimeSpan lockAge,
                                        out object lockId,
                                        out SessionStateActions actionFlags)
        {
            SqlDataReader reader;
            byte[] buf;
            MemoryStream stream = null;
            SessionStateStoreData item;
            SqlStateConnection conn = null;
            SqlCommand cmd = null;

            // Set default return values
            locked = false;
            lockId = null;
            lockAge = TimeSpan.Zero;
            actionFlags = 0;

            buf = null;

            conn = GetConnection();

            try
            {
                if (getExclusive)
                {
                    cmd = conn.TempGetExclusive;
                }
                else
                {
                    cmd = conn.TempGet;
                }

                cmd.Parameters[0].Value = id; // @id
                cmd.Parameters[1].Value = Convert.DBNull;   // @itemShort
                cmd.Parameters[2].Value = Convert.DBNull;   // @locked
                cmd.Parameters[3].Value = Convert.DBNull;   // @lockDate or @lockAge
                cmd.Parameters[4].Value = Convert.DBNull;   // @lockCookie
                cmd.Parameters[5].Value = Convert.DBNull;   // @actionFlags

                using (reader = cmd.ExecuteReader(CommandBehavior.Default))
                {
                    /* If the cmd returned data, we must read it all before getting out params */
                    if (reader.Read())
                    {
                        buf = (byte[])reader[0];
                    }
                }

                /* Check if value was returned */
                if (Convert.IsDBNull(cmd.Parameters[2].Value))
                {
                    return null;
                }

                /* Check if item is locked */
                locked = (bool)cmd.Parameters[2].Value;
                lockId = (int)cmd.Parameters[4].Value;

                if (locked)
                {
                    lockAge = new TimeSpan(0, 0, (int)cmd.Parameters[3].Value);

                    if (lockAge > new TimeSpan(365, 0, 0, 0))
                    {
                        lockAge = TimeSpan.Zero;
                    }
                    return null;
                }

                actionFlags = (SessionStateActions)cmd.Parameters[5].Value;

                if (buf == null)
                {
                    /* Get short item */
                    buf = (byte[])cmd.Parameters[1].Value;
                }

                // Done with the connection.
                DisposeOrReuseConnection(ref conn);

                using (stream = new MemoryStream(buf))
                {
                    item = DeserializeStoreData(context, stream);
                    _rqOrigStreamLen = (int)stream.Position;
                }
                return item;
            }
            finally
            {
                DisposeOrReuseConnection(ref conn);
            }
        }

        void DisposeOrReuseConnection(ref SqlStateConnection conn)
        {
            if (conn != null)
            {
                conn.Dispose();
            }
        }

        SqlStateConnection GetConnection()
        {
            return new SqlStateConnection(_connectionString);
        }

        static int SqlExecuteNonQuery(SqlCommand cmd, bool ignoreInsertPKException)
        {
            try
            {
                if (cmd.Connection.State != ConnectionState.Open)
                {
                    // reopen the connection
                    // (gets closed if a previous operation throwed a SQL exception with severity >= 20)
                    cmd.Connection.Open();
                }
                int result = cmd.ExecuteNonQuery();
                return result;
            }
            catch (SqlException e)
            {
                // if specified, ignore primary key violations
                if (IsInsertPKException(e, ignoreInsertPKException))
                {
                    return -1;
                }
                throw;
            }
        }

        static bool IsInsertPKException(SqlException ex, bool ignoreInsertPKException)
        {
            // If the severity is greater than 20, we have a serious error.
            // The server usually closes the connection in these cases.
            if (ex != null &&
                 ex.Number == 2627 &&
                 ignoreInsertPKException)
            {

                // It's possible that two threads (from the same session) are creating the session
                // state, both failed to get it first, and now both tried to insert it.
                // One thread may lose with a Primary Key Violation error. If so, that thread will
                // just lose and exit gracefully.
                return true;
            }
            return false;
        }
        private void SerializeStoreData(SessionStateStoreData item, int initialStreamSize, out byte[] buf, out int length)
        {
            using (MemoryStream s = new MemoryStream(initialStreamSize))
            {
                bool hasItems = true;
                bool hasStaticObjects = true;

                BinaryWriter writer = new BinaryWriter(s);
                writer.Write(item.Timeout);

                if (item.Items == null || item.Items.Count == 0)
                {
                    hasItems = false;
                }
                writer.Write(hasItems);

                if (item.StaticObjects == null || item.StaticObjects.NeverAccessed)
                {
                    hasStaticObjects = false;
                }
                writer.Write(hasStaticObjects);

                if (hasItems)
                {
                    ((SessionStateItemCollection)item.Items).Serialize(writer);
                }

                if (hasStaticObjects)
                {
                    item.StaticObjects.Serialize(writer);
                }

                // Prevent truncation of the stream
                writer.Write(unchecked((byte)0xff));
                buf = s.GetBuffer();
                length = (int)s.Length;
            }

        }

        private static SessionStateStoreData DeserializeStoreData(HttpContext context, Stream stream)
        {

            int timeout;
            SessionStateItemCollection sessionItems;
            bool hasItems;
            bool hasStaticObjects;
            HttpStaticObjectsCollection staticObjects;
            Byte eof;

            try
            {
                BinaryReader reader = new BinaryReader(stream);
                timeout = reader.ReadInt32();
                hasItems = reader.ReadBoolean();
                hasStaticObjects = reader.ReadBoolean();

                if (hasItems)
                {
                    sessionItems = SessionStateItemCollection.Deserialize(reader);
                }
                else
                {
                    sessionItems = new SessionStateItemCollection();
                }

                if (hasStaticObjects)
                {
                    staticObjects = HttpStaticObjectsCollection.Deserialize(reader);
                }
                else
                {
                    staticObjects = SessionStateUtility.GetSessionStaticObjects(context);
                }

                eof = reader.ReadByte();
                if (eof != 0xff)
                {
                    throw new HttpException("Invalid Session State");
                }
            }
            catch (EndOfStreamException)
            {
                throw new HttpException("Invalid Session State");
            }

            return new SessionStateStoreData(sessionItems, staticObjects, timeout);
        }

        class SqlStateConnection : IDisposable {
            private readonly string _connectionString;
            SqlConnection   _sqlConnection;
            SqlCommand      _cmdTempGet;
            SqlCommand      _cmdTempGetExclusive;
            SqlCommand      _cmdTempReleaseExclusive;
            SqlCommand      _cmdTempInsertShort;
            SqlCommand      _cmdTempInsertLong;
            SqlCommand      _cmdTempUpdateShort;
            SqlCommand      _cmdTempUpdateShortNullLong;
            SqlCommand      _cmdTempUpdateLong;
            SqlCommand      _cmdTempUpdateLongNullShort;
            SqlCommand      _cmdTempRemove;
            SqlCommand      _cmdTempResetTimeout;
            SqlCommand      _cmdTempInsertUninitializedItem;

            public SqlStateConnection(string connectionString) {
                _connectionString = connectionString;
                _sqlConnection = new SqlConnection(_connectionString);

                try
                {
                    _sqlConnection.Open();
                }
                catch (Exception e)
                {
                    ClearConnectionAndThrow(e);
                }

            }

            void ClearConnectionAndThrow(Exception e) {
                _sqlConnection = null;
                throw e;
            }

            internal SqlCommand TempGet {
                get {
                    if (_cmdTempGet == null) {
                        SqlParameter p;

                        _cmdTempGet = new SqlCommand("dbo.TempGetStateItem3", _sqlConnection);
                        _cmdTempGet.CommandType = CommandType.StoredProcedure;

                        _cmdTempGet.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 80));
                        p = _cmdTempGet.Parameters.Add(new SqlParameter("@itemShort", SqlDbType.VarBinary, ITEM_SHORT_LENGTH));
                        p.Direction = ParameterDirection.Output;
                        p = _cmdTempGet.Parameters.Add(new SqlParameter("@locked", SqlDbType.Bit));
                        p.Direction = ParameterDirection.Output;
                        p = _cmdTempGet.Parameters.Add(new SqlParameter("@lockAge", SqlDbType.Int));
                        p.Direction = ParameterDirection.Output;
                        p = _cmdTempGet.Parameters.Add(new SqlParameter("@lockCookie", SqlDbType.Int));
                        p.Direction = ParameterDirection.Output;
                        p = _cmdTempGet.Parameters.Add(new SqlParameter("@actionFlags", SqlDbType.Int));
                        p.Direction = ParameterDirection.Output;
                    }

                    return _cmdTempGet;
                }
            }

            internal SqlCommand TempGetExclusive {
                get {
                    if (_cmdTempGetExclusive == null) {
                        SqlParameter p;

                        _cmdTempGetExclusive = new SqlCommand("dbo.TempGetStateItemExclusive3", _sqlConnection);
                        _cmdTempGetExclusive.CommandType = CommandType.StoredProcedure;

                        _cmdTempGetExclusive.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 80));
                        p = _cmdTempGetExclusive.Parameters.Add(new SqlParameter("@itemShort", SqlDbType.VarBinary, ITEM_SHORT_LENGTH));
                        p.Direction = ParameterDirection.Output;
                        p = _cmdTempGetExclusive.Parameters.Add(new SqlParameter("@locked", SqlDbType.Bit));
                        p.Direction = ParameterDirection.Output;
                        p = _cmdTempGetExclusive.Parameters.Add(new SqlParameter("@lockAge", SqlDbType.Int));
                        p.Direction = ParameterDirection.Output;
                        p = _cmdTempGetExclusive.Parameters.Add(new SqlParameter("@lockCookie", SqlDbType.Int));
                        p.Direction = ParameterDirection.Output;
                        p = _cmdTempGetExclusive.Parameters.Add(new SqlParameter("@actionFlags", SqlDbType.Int));
                        p.Direction = ParameterDirection.Output;
                    }

                    return _cmdTempGetExclusive;
                }
            }

            internal SqlCommand TempReleaseExclusive {
                get {
                    if (_cmdTempReleaseExclusive == null) {
                        /* ReleaseExlusive */
                        _cmdTempReleaseExclusive = new SqlCommand("dbo.TempReleaseStateItemExclusive", _sqlConnection);
                        _cmdTempReleaseExclusive.CommandType = CommandType.StoredProcedure;
                        _cmdTempReleaseExclusive.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 80));
                        _cmdTempReleaseExclusive.Parameters.Add(new SqlParameter("@lockCookie", SqlDbType.Int));
                    }

                    return _cmdTempReleaseExclusive;
                }
            }

            internal SqlCommand TempInsertLong {
                get {
                    if (_cmdTempInsertLong == null) {
                        _cmdTempInsertLong = new SqlCommand("dbo.TempInsertStateItemLong", _sqlConnection);
                        _cmdTempInsertLong.CommandType = CommandType.StoredProcedure;
                        _cmdTempInsertLong.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 80));
                        _cmdTempInsertLong.Parameters.Add(new SqlParameter("@itemLong", SqlDbType.Image, 8000));
                        _cmdTempInsertLong.Parameters.Add(new SqlParameter("@timeout", SqlDbType.Int));
                    }

                    return _cmdTempInsertLong;
                }
            }

            internal SqlCommand TempInsertShort {
                get {
                    /* Insert */
                    if (_cmdTempInsertShort == null) {
                        _cmdTempInsertShort = new SqlCommand("dbo.TempInsertStateItemShort", _sqlConnection);
                        _cmdTempInsertShort.CommandType = CommandType.StoredProcedure;
                        _cmdTempInsertShort.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 80));
                        _cmdTempInsertShort.Parameters.Add(new SqlParameter("@itemShort", SqlDbType.VarBinary, ITEM_SHORT_LENGTH));
                        _cmdTempInsertShort.Parameters.Add(new SqlParameter("@timeout", SqlDbType.Int));
                    }

                    return _cmdTempInsertShort;
                }
            }

            internal SqlCommand TempUpdateLong {
                get {
                    if (_cmdTempUpdateLong == null) {
                        _cmdTempUpdateLong = new SqlCommand("dbo.TempUpdateStateItemLong", _sqlConnection);
                        _cmdTempUpdateLong.CommandType = CommandType.StoredProcedure;
                        _cmdTempUpdateLong.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 80));
                        _cmdTempUpdateLong.Parameters.Add(new SqlParameter("@itemLong", SqlDbType.Image, 8000));
                        _cmdTempUpdateLong.Parameters.Add(new SqlParameter("@timeout", SqlDbType.Int));
                        _cmdTempUpdateLong.Parameters.Add(new SqlParameter("@lockCookie", SqlDbType.Int));
                    }

                    return _cmdTempUpdateLong;
                }
            }

            internal SqlCommand TempUpdateShort {
                get {
                    /* Update */
                    if (_cmdTempUpdateShort == null) {
                        _cmdTempUpdateShort = new SqlCommand("dbo.TempUpdateStateItemShort", _sqlConnection);
                        _cmdTempUpdateShort.CommandType = CommandType.StoredProcedure;
                        _cmdTempUpdateShort.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 80));
                        _cmdTempUpdateShort.Parameters.Add(new SqlParameter("@itemShort", SqlDbType.VarBinary, ITEM_SHORT_LENGTH));
                        _cmdTempUpdateShort.Parameters.Add(new SqlParameter("@timeout", SqlDbType.Int));
                        _cmdTempUpdateShort.Parameters.Add(new SqlParameter("@lockCookie", SqlDbType.Int));
                    }

                    return _cmdTempUpdateShort;

                }
            }

            internal SqlCommand TempUpdateShortNullLong {
                get {
                    if (_cmdTempUpdateShortNullLong == null) {
                        _cmdTempUpdateShortNullLong = new SqlCommand("dbo.TempUpdateStateItemShortNullLong", _sqlConnection);
                        _cmdTempUpdateShortNullLong.CommandType = CommandType.StoredProcedure;
                        _cmdTempUpdateShortNullLong.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 80));
                        _cmdTempUpdateShortNullLong.Parameters.Add(new SqlParameter("@itemShort", SqlDbType.VarBinary, ITEM_SHORT_LENGTH));
                        _cmdTempUpdateShortNullLong.Parameters.Add(new SqlParameter("@timeout", SqlDbType.Int));
                        _cmdTempUpdateShortNullLong.Parameters.Add(new SqlParameter("@lockCookie", SqlDbType.Int));
                    }

                    return _cmdTempUpdateShortNullLong;
                }
            }

            internal SqlCommand TempUpdateLongNullShort {
                get {
                    if (_cmdTempUpdateLongNullShort == null) {
                        _cmdTempUpdateLongNullShort = new SqlCommand("dbo.TempUpdateStateItemLongNullShort", _sqlConnection);
                        _cmdTempUpdateLongNullShort.CommandType = CommandType.StoredProcedure;
                        _cmdTempUpdateLongNullShort.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 80));
                        _cmdTempUpdateLongNullShort.Parameters.Add(new SqlParameter("@itemLong", SqlDbType.Image, 8000));
                        _cmdTempUpdateLongNullShort.Parameters.Add(new SqlParameter("@timeout", SqlDbType.Int));
                        _cmdTempUpdateLongNullShort.Parameters.Add(new SqlParameter("@lockCookie", SqlDbType.Int));
                    }

                    return _cmdTempUpdateLongNullShort;
                }
            }

            internal SqlCommand TempRemove {
                get {
                    if (_cmdTempRemove == null) {
                        /* Remove */
                        _cmdTempRemove = new SqlCommand("dbo.TempRemoveStateItem", _sqlConnection);
                        _cmdTempRemove.CommandType = CommandType.StoredProcedure;
                        _cmdTempRemove.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 80));
                        _cmdTempRemove.Parameters.Add(new SqlParameter("@lockCookie", SqlDbType.Int));

                    }

                    return _cmdTempRemove;
                }
            }

            internal SqlCommand TempInsertUninitializedItem {
                get {
                    if (_cmdTempInsertUninitializedItem == null) {
                        _cmdTempInsertUninitializedItem = new SqlCommand("dbo.TempInsertUninitializedItem", _sqlConnection);
                        _cmdTempInsertUninitializedItem.CommandType = CommandType.StoredProcedure;
                        _cmdTempInsertUninitializedItem.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 80));
                        _cmdTempInsertUninitializedItem.Parameters.Add(new SqlParameter("@itemShort", SqlDbType.VarBinary, ITEM_SHORT_LENGTH));
                        _cmdTempInsertUninitializedItem.Parameters.Add(new SqlParameter("@timeout", SqlDbType.Int));
                    }

                    return _cmdTempInsertUninitializedItem;
                }
            }

            internal SqlCommand TempResetTimeout {
                get {
                    if (_cmdTempResetTimeout == null) {
                        /* ResetTimeout */
                        _cmdTempResetTimeout = new SqlCommand("dbo.TempResetTimeout", _sqlConnection);
                        _cmdTempResetTimeout.CommandType = CommandType.StoredProcedure;
                        _cmdTempResetTimeout.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 80));
                    }

                    return _cmdTempResetTimeout;
                }
            }

            public void Dispose() {
                if (_sqlConnection != null) {
                    _sqlConnection.Close();
                    _sqlConnection = null;
                }
            }
        }
    }
}
