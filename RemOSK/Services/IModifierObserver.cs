namespace RemOSK.Services
{
    public interface IModifierObserver
    {
        void OnModifierStateChanged(ModifierStateManager manager);
    }
}
