namespace Game.Datas.GMEntities {

    public class BaseEntity
    {
        public RGameComponent uRoomGame;

        public TransformComponent uTransform;

        public WGameComponent uWorldGame;

        public StatusComponent uStatus;

        public NavComponent uNav;

        public AStarComponent uAStarNav;

        public PropsComponent uProps;

        public SkillAndBuffComponent uSkillAndBuff;

        public AOIComponent uAoi;

        public RVOComponent uRvo;
    }

    public class GM_PlayerEntity : BaseEntity {

        public PlayerComponent uPlayer;

        public TaskComponent uTask; 

        public BackpackComponent uBackpack;

        public ItemComponent uItem;

        public CharacterComponent uCharacter;

        public GachaComponent uGacha;

        public FormationComponent uFormation;
        public RedDotComponent uRedDot;

    }

    // public class GM_NPCEntity : BaseEntity {}
    // public class GM_MonestEntity : BaseEntity {}

}
