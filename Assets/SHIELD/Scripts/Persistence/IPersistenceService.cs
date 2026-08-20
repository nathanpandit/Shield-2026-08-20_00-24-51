namespace ShieldGame
{
    public interface IPersistenceService
    {
        int BestScore { get; set; }
        bool SoundEnabled { get; set; }
        bool HapticsEnabled { get; set; }
        bool TutorialCompleted { get; set; }
        void Save();
    }
}
