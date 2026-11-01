# Reference Demo

These three ScriptableObject assets form a chain:

`IndirectReferencer -> DirectReferencer -> Target`

1. Select **Target** in the Project window.
2. Choose **Assets > Asset Delete Guard > Review References**.
3. The direct list includes **DirectReferencer**. Expand **Indirect impact** to see **IndirectReferencer**.
4. Select Target and DirectReferencer together. The remaining direct source is IndirectReferencer; references entirely inside the selection are excluded.
5. To try deletion, work with a copy of this sample in a disposable project. Use **Review and Delete**, inspect the inventory and scope limitations, acknowledge them and confirm. Deleting Target intentionally breaks the sample reference.

The sample is Editor-only and requires no render pipeline or additional product. For UPM installations, import it once from the package's **Samples** section. The `.unitypackage` preview includes it under `AssetDeleteGuard/Samples/ReferenceDemo`. Reimporting the same sample can restore overwritten sample files; restore a deliberately deleted file together with its original `.meta` to preserve its GUID.

한국어: `Target`을 선택하고 **Assets > Asset Delete Guard > Review References**를 실행하면 `DirectReferencer`가 직접 참조로, `IndirectReferencer`가 간접 영향으로 표시됩니다. 실제 삭제 실습은 별도 테스트 프로젝트에서 진행하세요. 이 샘플의 스크립트와 데이터는 Editor 전용입니다.
