LAB8 balance INI patch

Adds two editable [Gameplay] values to mod_settings.ini:

HeatGainPerHitPercent=1.25
ExtremeHeatDrainInterval=0.75

Existing setting remains:
KiryuDamageMultiplier=0.4   (or your preferred value)

Recommended first test:
[Gameplay]
KiryuDamageMultiplier=0.4
HeatGainPerHitPercent=1.25
ExtremeHeatDrainInterval=0.75

HeatGainPerHitPercent examples:
4.0  = old GOLD value (4% max Heat per hit)
1.5  = slower
1.25 = recommended first test
1.0  = very slow
0    = no hit-based Heat gain

ExtremeHeatDrainInterval: seconds between each 1-point drain tick.
Larger = R2/Extreme Heat lasts longer. Smaller = drains faster.

This package is source/build-ready; it must be compiled once. After installing that compiled DLL, future balance changes only require editing mod_settings.ini and restarting the game.
