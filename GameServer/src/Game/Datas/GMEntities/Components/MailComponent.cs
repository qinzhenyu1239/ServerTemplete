using Framework.Core.Net;
namespace Game.Datas.GMEntities
{
    public sealed class MailComponent
    {
        public IdSession Session;
        public long Version;
        public int Count;
        public int UnreadCount;
        public static void Init(GM_PlayerEntity entity) => Reset(entity);
        public static void Reset(GM_PlayerEntity entity) => entity.uMail = new MailComponent();
    }
}
