using StardewModdingAPI;

namespace SuperAardvark.AntiSocial
{
    public class ModEntry : Mod
    {
        public override void Entry(IModHelper helper)
        {
            AntiSocialManager.DoSetupIfNecessary(this);
        }
    }
}
