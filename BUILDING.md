# Build / 빌드

Requires Windows, Cities: Skylines II and the official Modding Toolchain configured through the game, Node.js 18+ and a compatible .NET SDK. Game assemblies are resolved by the toolchain and are not included in this repository.

```powershell
cd ui
npm ci
cd ..
./build.ps1
```

Output: `package/`. Copy its contents into a local mod directory only with the game closed. Do not enable both a local copy and a subscribed copy of Apartment Plan at once.

Windows, 게임 내에서 설정한 공식 Modding Toolchain, Node.js 18 이상 및 호환 .NET SDK가 필요합니다. 게임 DLL은 포함하지 않습니다. 위 명령으로 빌드하면 `package/`에 결과가 생성됩니다. 게임 종료 상태에서만 로컬 설치하고, 로컬판과 구독판을 동시에 활성화하지 마세요.

The required native road assets are distributed separately as AP Invisible Road Pack (Paradox Mods 165049). They are not built by this repository.

The diagnostic server deployment and private configuration are not part of this repository. The consent flow and client payload are available in `src/DiagnosticSharing.cs` and `localization.cjs`.
For the published 0.1.118 logging configuration, run `./build.ps1 -SkipLayoutTests -PublicRelease`. Normal local builds retain stage timing; public builds disable it. Full layout tests also need their referenced local fixtures, which are not included.
