using System.Runtime.InteropServices;

namespace Network.Node.Session
{
    [ComVisible(true)]
    [Guid("1158E1D9-EA33-4B54-9740-9A624E5CFBC1")]
    public interface ISessionStateClient
    {
        void GetItem();
        void GetItemExclusive();
        void SetAndReleaseItemExclusive();
    }
}
