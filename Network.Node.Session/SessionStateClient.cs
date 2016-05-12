using System;
using System.Collections.Specialized;
using System.Configuration;
using System.EnterpriseServices;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Web;
using System.Web.SessionState;
using ASPTypeLibrary;

namespace Network.Node.Session
{
    [ComVisible(true)]
    [Guid("9B8274EC-331C-4495-8DFA-162B7599E444")]
    [ClassInterface(ClassInterfaceType.None)]
    public class SessionStateClient : ISessionStateClient, IDisposable
    {
        protected bool _isExclusive = false;
        protected IRequest _request;
        protected ISessionObject _session;
        protected HttpContext _context;
        protected string _sessionId;

        protected bool _locked;
        protected TimeSpan _lockAge;
        protected object _lockId;
        protected SessionStateActions _actionFlags;

        protected TimeSpan _executionTimeout = new TimeSpan(0, 1, 50);
        protected int _sessionTimeout = 1200;

        private bool _disposed;
        private Session _store;
        private bool _newItem;

        #region ISessionStateClient Members

        public void GetItem()
        {
            GetItemInternal(false);
        }

        public void GetItemExclusive()
        {
            GetItemInternal(true);
        }

        public void SetAndReleaseItemExclusive()
        {
            Init();

            if (_isExclusive)
            {
                SessionStateItemCollection sessionItems = new SessionStateItemCollection();
                foreach (string key in _session.Contents)
                {
                    sessionItems[key] = _session[key];
                }

                SessionStateStoreData data = new SessionStateStoreData(sessionItems, null, 20);
                _store.SetAndReleaseItemExclusive(_context, _sessionId, data, _lockId, _newItem);
                _isExclusive = false;
            }
        }

        #endregion

        protected void GetItemInternal(bool isExclusive)
        {
            Init();

            SessionStateStoreData data;
            if (isExclusive)
            {
                _isExclusive = isExclusive;
                while (true)
                {
                    data = _store.GetItemExclusive(_context, _sessionId, out _locked, out _lockAge, out _lockId, out _actionFlags);
                    if (data == null)
                    {
                        if (_locked)
                        {
                            if (_lockAge > _executionTimeout)
                            {
                                _store.ReleaseItemExclusive(_context, _sessionId, _lockId);
                            }
                            else
                            {
                                System.Threading.Thread.Sleep(500);
                            }
                        }
                        else
                        {
                            data = _store.CreateNewStoreData(_context, _sessionTimeout);
                            _newItem = true;
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            else
            {
                data = _store.GetItem(_context, _sessionId, out _locked, out _lockAge, out _lockId, out _actionFlags);
                if (data == null)
                {
                    data = _store.CreateNewStoreData(_context, _sessionTimeout);
                    _newItem = true;
                }
            }
            ISessionStateItemCollection sessionItems = data.Items;

            foreach (string key in sessionItems.Keys)
            {
                _session[key] = sessionItems[key];
            }              

        }

        protected void Init()
        {
            if (_request == null)
            { 
                _request = (IRequest)ContextUtil.GetNamedProperty("Request");
                _session = (ISessionObject)ContextUtil.GetNamedProperty("Session");

                var cookie = (IReadCookie)_request.Cookies["ASP.NET_SessionId"];
                //http://msdn.microsoft.com/en-us/library/ms525056(VS.90).aspx
                _sessionId = cookie[Missing.Value]; //VR_ERROR with DISP_E_PARAMNOTFOUND, http://www.informit.com/articles/article.aspx?p=27219&seqNum=8

                var wr = new AspWorkerRequest(_request);
                _context = new HttpContext(wr);
                _store = new Session();
                var sessionConnectionString = ConfigurationManager.ConnectionStrings["Session"];
                if (sessionConnectionString == null)
                {
                    throw new Exception("No connection string found. Please add a connection string called 'Session' to your machine.config file");
                }
                _store.Initialize("SharedSession", new NameValueCollection { { "connectionString", sessionConnectionString.ConnectionString } });
            }
        }


        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);

            // Use SupressFinalize in case a subclass
            // of this type implements a finalizer.
            GC.SuppressFinalize(this);
        }

        #endregion

        protected virtual void Dispose(bool disposing)
        {
            // If you need thread safety, use a lock around these 
            // operations, as well as in your methods that use the resource.
            if (_disposed) return;
            if (disposing && _isExclusive)
            {
                SetAndReleaseItemExclusive();
            }

            // Indicate that the instance has been disposed.
            _isExclusive = false;
            _disposed = true;
        }

    }
}
