# Optional category teaching examples

Build: `dotnet build examples/CategoryExamples/CategoryExamples.csproj -c Release -p:ChainedBeastsGameDir="D:\Games\Chained Beasts"`.

Requires Foundation 0.8.x and the installed generated interop. No TermsAndContusions dependency. GUID `example.chainedbeasts.categoryexamples`; distinct equipment 2470, injury 12010 and surgery 12020 claims. These are demonstration identities checked against the inspected native build, not globally reserved IDs. Rename GUID/assembly/namespace/keys/IDs before publishing a copy.

The complete loader installs Practice Wraps (+5% equipped recovery, price 35), Practice Limp (2/4/6% effective movement reduction) and Practice Graft (+5% run recovery, price 20). It uses public Foundation registration, modifiers, localization, shared surgery completion and checkpoint services. No fixed marker slots or duplicate ownership store.

Normal packages/deployment exclude this DLL. To manually install, close the game and back up the matching DLL/config/user-data set; copy only CategoryExamples.dll beside Foundation. Never copy all bin files. All live presentation/acquisition/effects/save behavior remains unverified. Follow the [category tutorials](../../docs/foundation/SEPARATE_MOD.md), [installation/rollback](../../docs/INSTALLATION.md) and [save/restore guidance](../../docs/foundation/PERSISTENCE.md).
