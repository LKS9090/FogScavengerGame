# ML-Agents package source

`ml-agents-release21.tgz` is a portable local Unity Package Manager archive, not a trained model.

- Source: https://github.com/Unity-Technologies/ml-agents
- Release 21 commit: `7a03145ae48ad354821bd89e0243d99332149ace`.
- Tree: `com.unity.ml-agents`; package version `3.0.0-exp.1`.
- Generated without source edits using `git archive --format=tar.gz --prefix=package/ COMMIT:com.unity.ml-agents`.
- SHA256: `49540D4202760BEF6169B0DF295DA808780245F24BD5C3B27BC7720518DD6509`.
- The original LICENSE.md and Third Party Notices.md are inside the archive.
- Sentis `1.2.0-exp.2` is resolved as its dependency; Python still uses the previously verified fog-mlagents environment.

The unused template `cn.unity.uos.launcher` dependency was removed because its Google.Protobuf assembly conflicts with Release 21/Sentis's packed assembly (CS0433). Its old Resources settings asset is preserved outside Unity's import tree in Training/Compatibility/LegacyUOS. No game script depended on UOS APIs. This does not change account or system security settings.
