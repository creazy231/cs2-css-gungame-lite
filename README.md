# TODO

# Resources
https://github.com/mouminoux/cs-16-server/blob/9851ccf9a15ba66635152db4c2590203fb9b308e/cstrike/addons/amxmodx/configs/gungame.cfg#L78

# Fix Message
```csharp
public static void PlayerCenterHtml(CCSPlayerController player, string message, int duration = 0)
{
  if (duration > 0)
  {
    TimerRepeat.RepeatFor(duration, .05f, () => { PlayerCenterHtml(player, message); });
   }
  else
  {
    player.PrintToCenterHtml(message);
  }
}

public static void RepeatFor(float duration, float delay, Action action)
{
  if (duration <= 0)
    return;

  action?.Invoke();

  MyPlugin.Instance.AddTimer(delay, () => RepeatFor(duration - delay, delay, action));
}
```

## TODO
- https://github.com/PhantomYopta/LiteMapChooser
- 