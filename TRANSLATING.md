# Translation

`lang/en-US.json` and `lang/ko-KR.json` are UTF-8 flat key/value dictionaries embedded into the DLL at build time and used by the mod without translation dependencies. UI strings use cs2/l10n with English fallback. Options locale IDs are derived from the installed ModSetting API.

To prepare another language, copy en-US.json to your locale ID filename and edit values only. Keep AP.* keys unchanged. Keep a separate backup of your translation; a local build or installation may overwrite shipped languages. No file is submitted online automatically.

I18NEveryWhere can read installed mod lang dictionaries; use its language/Overwrite controls for replacing built-in entries and review Restrict when a language pack has duplicate keys. That integration was not exercised in-game for this mod. English and Korean work through the mod's native locale registration.

