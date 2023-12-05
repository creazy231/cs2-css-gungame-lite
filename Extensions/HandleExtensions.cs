using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Utils;

namespace GunGameLite.Extensions
{
    public static class HandleExtensions
    {
        public static T? GetValueOrNull<T>(this CHandle<T> handle) where T : NativeEntity
        {
            return !handle.IsValid ? null : handle.Value;
        }
    }
}