/// <summary>Unity 대본의 강타입 사건 명령. 웹 CSV 표현은 로드 경계에서만 해석한다.</summary>
public enum OutsideStoryCommand
{
    None, SharedChoice, ShopIntroComplete, ShopOpen, ShopGreeting, ShopItems, ShopAllowance, GrantAllowance, ShopQuiz,
    WardrobeChoice, ThugChoice, BeggarChoice, CatChoice, HomeCatChoice, RepairIntroChoice, RepairChoice,
    GuardChoice, BossFirstChoice, BossRepeatChoice, MeetBoss, GrantWildDog,
    RepairElevator, AdoptCat, RevealCat, PetCat, FeedCat, NeighborEnter, NeighborExit, ChrisEnter, CatMeow,
    SamhoDepart, NotionApproach, NotionDealerExit, NotionSamhoExit, ThugPunch, ThugDepart, DoorCue,
    NpcSequence
}

