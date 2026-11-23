using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 갱단 구역 드럼통 불(#80)이 병합 중 빠지지 않았는지 검사한다.
/// 불빛·불꽃은 드럼 앞면 뒤에서 위로 솟고, 불씨만 앞에 그려져 배경 그림의 빨간 자리 표시를 덮는다.
/// </summary>
public sealed class OutsideFireVfxTests
{
    private const string OutsidePath = "Assets/01.Game/01.Scenes/OutSide.unity";
    private const string Materials = "Assets/01.Game/03.Content/03.Vfx/02.Materials/";

    [Test]
    public void GangDrumKeepsItsFireEmbersGlowAndFlickeringLight()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var scene = SceneManager.GetSceneByPath(OutsidePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(OutsidePath, OpenSceneMode.Additive);
        try
        {
            var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToList();
            Transform Find(string name) => all.Single(value => value.name == name);
            ParticleSystemRenderer Particles(string name, string material)
            {
                var renderer = Find(name).GetComponent<ParticleSystemRenderer>();
                Assert.That(renderer, Is.Not.Null, name);
                Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial), Is.EqualTo(Materials + material + ".mat"), name);
                return renderer;
            }

            var fire = Particles("Fire", "FX_Mat_Fire");
            var glow = Particles("FireGlow", "FX_Mat_FireGlow_01");
            var embers = Particles("FireEmbers", "FX_Mat_FireEmber_01");
            Assert.That(glow.transform.parent, Is.SameAs(fire.transform));
            Assert.That(embers.transform.parent, Is.SameAs(fire.transform));

            var drum = Find("B_Obects_2").GetComponent<SpriteRenderer>();
            Assert.That(Mathf.Abs(fire.transform.position.x - drum.transform.position.x), Is.LessThan(.05f), "불은 드럼통 위에 있어야 한다.");
            foreach (var renderer in new Renderer[] { glow, fire, embers })
                Assert.That(renderer.sortingLayerID, Is.EqualTo(drum.sortingLayerID), renderer.name);
            Assert.That(glow.sortingOrder, Is.LessThan(fire.sortingOrder));
            Assert.That(fire.sortingOrder, Is.LessThan(drum.sortingOrder), "불꽃은 드럼 앞면 뒤에서 솟는다.");
            Assert.That(embers.sortingOrder, Is.GreaterThan(drum.sortingOrder), "불씨는 드럼 앞에 날린다.");
            Assert.That(drum.sortingOrder, Is.GreaterThan(Find("B_Obects_1").GetComponent<SpriteRenderer>().sortingOrder),
                "드럼 앞면이 배경 그림(빨간 자리 표시)보다 위에 그려져야 한다.");

            var light = Find("Light_Point_Fire");
            Assert.That(light.GetComponent<Light2D>(), Is.Not.Null);
            Assert.That(light.GetComponent<FireLightFlicker>(), Is.Not.Null);
            Assert.That(light.GetComponent<Light2D>().targetSortingLayers, Does.Contain(drum.sortingLayerID));
            Assert.That(Mathf.Abs(light.position.x - drum.transform.position.x), Is.LessThan(.05f));
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }
}
