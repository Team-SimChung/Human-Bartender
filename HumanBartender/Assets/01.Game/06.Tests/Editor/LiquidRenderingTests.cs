using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class LiquidRenderingTests
{
    const string ShaderFolder = "Assets/01.Game/03.Content/01.ArtResources/07.Legacy/03.Crafting/03.Minigames/04.Pour/02.Shaders/";
    GameObject root;
    Camera camera;
    SphLiquidRenderer renderer;
    LiquidProfile profile;
    readonly List<SphParticle> particles = new List<SphParticle>();

    [UnityTest]
    public IEnumerator RealPourPrefabRendersBeerAndReleasesGpuResourcesInPlayMode()
    {
        RequireGpu();
        yield return new EnterPlayMode();
        yield return ExerciseRealPrefab(false);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator BeerTwelveOuncesFitsBelowTheRimAndRealOverpourStillOverflows()
    {
        RequireGpu();
        yield return new EnterPlayMode();
        yield return ExerciseRealPrefab(true);
        yield return new ExitPlayMode();
    }

    static IEnumerator ExerciseRealPrefab(bool fillTarget)
    {
        var stageRoot = new GameObject("liquid runtime stage");
        stageRoot.SetActive(false);
        stageRoot.transform.position = new Vector3(700,600,0);
        var stage = stageRoot.AddComponent<CraftGimmickStage>();
        var cameraObject = new GameObject("camera",typeof(Camera));
        cameraObject.transform.SetParent(stageRoot.transform,false);
        var cam = cameraObject.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 1.8f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f,0.16f,0.2f);
        var target = new RenderTexture(768,768,24,RenderTextureFormat.ARGB32);
        target.Create();
        cam.targetTexture = target;
        Set(stage,"stageCamera",cam);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/01.Game/03.Content/08.Prefabs/01.Crafting/02.Minigames/03.Pour/NewPourGame.prefab");
        var instance = Object.Instantiate(prefab,stageRoot.transform);
        instance.transform.localPosition = Vector3.zero;
        var manager = instance.GetComponentInChildren<PourManager>(true);
        Set(manager,"drivenByRunner",true);
        Set(manager,"pourIngredientId","beer");
        Set(manager,"pourStep",new GimmickStep(ECraftGimmick.Pour,"beer",12f,ENewUnit.Oz,true));
        Set(manager,"showTargetHeight",fillTarget);
        var session = new CraftSession("beer");
        session.Actual.SetGlass("mug");
        Set(manager,"craftContext",CraftContext.From(session));
        var balance = ScriptableObject.CreateInstance<NewBalanceDataSO>();
        balance.balanceData = new NewBalanceDataBase { Config = new NewBalanceConfig { PourEmitRateMlPerSec=70,UnitOzToMl=30 } };
        Set(manager,"balanceData",balance);
        var glass = Get<Transform>(manager,"glassCenter");
        cam.transform.position = glass.position + new Vector3(0,0.25f,-10);
        var gpu = Get<SphLiquidRenderer>(manager,"liquidRenderer");
        var bottle = Get<BottleTiltController>(manager,"bottle");
        float previousCapture = Time.captureDeltaTime;
        Time.captureDeltaTime = 1f/60;
        var mutedListeners = new List<AudioListener>();
        foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (listener.enabled) { listener.enabled = false; mutedListeners.Add(listener); }
        cameraObject.AddComponent<AudioListener>();
        try
        {
            stageRoot.SetActive(true);
            yield return null;
            Assert.That(Get<LiquidAppearance>(gpu,"appearance").carbonation, Is.GreaterThan(0));
            Assert.That(Get<Color>(gpu,"liquidColor").a, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(Get<SphSimulation>(manager,"simulation").SynchronizeTransforms, Is.False);
            bottle.BeginDrag(bottle.PourPivotWorldPosition + Quaternion.Euler(0,0,-110) * Vector3.down * 2);
            if (fillTarget)
            {
                for (int i=0;i<1900 && manager.PouredInTargetUnit < 12f;i++)
                {
                    yield return null;
                    Assert.That(HasSpilled(manager,glass),Is.False,"Beer must not overflow before reaching 12oz.");
                }
                Assert.That(manager.PouredInTargetUnit,Is.InRange(12f,12.1f));
                Save(target,"runtime-beer-12oz");
            }
            else for (int i=0;i<160;i++) yield return null;
            Assert.That(Get<SphSimulation>(manager,"simulation").ActiveParticles.Count, Is.GreaterThan(30));
            Assert.That(Get<RenderTexture>(gpu,"fieldTexture"), Is.Not.Null);
            Assert.That(MeanRed(Get<RenderTexture>(gpu,"fieldTexture")), Is.GreaterThan(0.001f));
            Assert.That(Get<int>(gpu,"renderedFrame"), Is.GreaterThan(0), "Must render through the actual URP camera callback.");
            if (!fillTarget) Save(target,"runtime-beer-pouring");
            bottle.EndDrag();
            for (int i=0;i<480;i++) yield return null;
            var simulation = Get<SphSimulation>(manager,"simulation");
            float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity, energy = 0;
            foreach (var particle in simulation.ActiveParticles)
            {
                minimum = Mathf.Min(minimum,particle.pos.y);
                maximum = Mathf.Max(maximum,particle.pos.y);
                energy += particle.velocity.sqrMagnitude;
            }
            TestContext.WriteLine($"After pour: particles={simulation.ActiveParticles.Count}, y={minimum}..{maximum}, energy={energy}, restDensity={Get<float>(simulation, "restDensity")}, dt={Time.deltaTime}, angle={bottle.CurrentAngle}, viscosity={simulation.config.viscosity}, iterations={simulation.config.densityIterations}");
            Save(target,fillTarget ? "runtime-beer-12oz-settled" : "runtime-beer-settled");
            var left = new List<float>(); var right = new List<float>();
            foreach (var particle in simulation.ActiveParticles)
            {
                if (particle.pos.x < glass.position.x-0.15f) left.Add(particle.pos.y);
                if (particle.pos.x > glass.position.x+0.15f) right.Add(particle.pos.y);
            }
            left.Sort(); right.Sort();
            float gap = Mathf.Abs(left[(int)((left.Count-1)*0.95f)]-right[(int)((right.Count-1)*0.95f)]);
            TestContext.WriteLine($"actual slope={gap}, left={left.Count}, right={right.Count}, glass={glass.position}, scale={glass.lossyScale}, inside={Get<bool>(simulation.ActiveParticles[0],"insideContainer")}");
            Assert.That(gap, Is.LessThan(simulation.config.spacing*1.5f), "Actual prefab must settle to a level surface.");
            if (fillTarget)
            {
                Assert.That(HasSpilled(manager,glass),Is.False);
                float level = (left[(int)((left.Count-1)*0.95f)]+right[(int)((right.Count-1)*0.95f)])*0.5f;
                float marker = glass.Find("Target Liquid Height").position.y;
                TestContext.WriteLine($"12oz level={level}, marker={marker}, difference={Mathf.Abs(level-marker)}, particleMl={Get<float>(manager,"particleVolumeMl")}, quantity={manager.PouredInTargetUnit}");
                Assert.That(Mathf.Abs(level-marker),Is.LessThan(simulation.config.spacing*2.5f));
                Assert.That(Get<RenderTexture>(gpu,"surfaceRead"),Is.Not.Null);
                float center = Read(Get<RenderTexture>(gpu,"surfaceRead"),0.5f,0.5f).r;
                Assert.That(center,Is.LessThan(0.9f),"Keep room above the liquid for beer foam.");
                bottle.BeginDrag(bottle.PourPivotWorldPosition + Quaternion.Euler(0,0,-110)*Vector3.down*2);
                bool spilled = false;
                for (int i=0;i<1200 && manager.PouredInTargetUnit<18f;i++)
                {
                    yield return null;
                    spilled |= HasSpilled(manager,glass);
                }
                Assert.That(manager.PouredInTargetUnit,Is.GreaterThan(17.9f));
                Assert.That(spilled,Is.True,"Real overpour must remain possible; do not clamp to the recipe amount.");
                Save(target,"runtime-beer-overpour");
            }
            gpu.enabled = false;
            Assert.That(Get<RenderTexture>(gpu,"fieldTexture"), Is.Null);
            Assert.That(Get<GraphicsBuffer>(gpu,"particleBuffer"), Is.Null);
            gpu.enabled = true;
            for (int i=0;i<3;i++) yield return null;
            Assert.That(Get<RenderTexture>(gpu,"fieldTexture"), Is.Not.Null);
            Assert.That(Get<GraphicsBuffer>(gpu,"particleBuffer"), Is.Not.Null);
        }
        finally
        {
            Time.captureDeltaTime = previousCapture;
            foreach (var listener in mutedListeners) if (listener != null) listener.enabled = true;
            cam.targetTexture = null;
            Object.DestroyImmediate(stageRoot);
            Object.DestroyImmediate(balance);
            if (RenderTexture.active == target) RenderTexture.active = null;
            target.Release(); Object.DestroyImmediate(target);
        }
    }

    static bool HasSpilled(PourManager manager,Transform glass)
    {
        var extents = Get<Vector2>(manager,"glassInteriorHalfExtents");
        foreach (var particle in Get<SphSimulation>(manager,"simulation").ActiveParticles)
        {
            Vector3 local = glass.InverseTransformPoint(particle.pos);
            if (local.y < -extents.y-0.02f || (Mathf.Abs(local.x)>extents.x+0.04f && local.y<extents.y-0.1f)) return true;
        }
        return false;
    }

    [UnityTearDown]
    public IEnumerator LeavePlayModeAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [TearDown]
    public void Cleanup()
    {
        // EditMode의 일반 MonoBehaviour는 플레이 모드 수명주기 콜백을 받지 않는다.
        if (renderer != null && !Application.isPlaying)
        {
            Invoke(renderer,"OnDisable");
            Invoke(renderer,"OnDestroy");
        }
        if (root != null) Object.DestroyImmediate(root);
        if (profile != null) Object.DestroyImmediate(profile);
        particles.Clear();
    }

    [Test]
    public void CarbonationFoamAndOpacityRemainDifferentAcrossLiquids()
    {
        var beer = LiquidAppearance.ForIngredient("beer");
        var soda = LiquidAppearance.ForIngredient("soda_water");
        var gin = LiquidAppearance.ForIngredient("gin");
        var orange = LiquidAppearance.ForIngredient("orange_juice");
        Assert.That(beer.foamLifetime, Is.GreaterThan(soda.foamLifetime * 3));
        Assert.That(soda.carbonation, Is.GreaterThan(0));
        Assert.That(gin.carbonation, Is.Zero);
        Assert.That(orange.carbonation, Is.Zero);
        Assert.That(orange.turbidity, Is.GreaterThan(gin.turbidity));
        Assert.That(LiquidAppearance.ForIngredient("grenadine").viscosityScale,
            Is.GreaterThan(LiquidAppearance.ForIngredient("whiskey").viscosityScale));
        Color source = new Color(0.3f,0.4f,0.5f,1);
        Assert.That(gin.ResolveColor(source,0.18f,false).a, Is.EqualTo(0.18f));
        Assert.That(gin.ResolveColor(source,0.18f,true), Is.EqualTo(source));
        Assert.That(LiquidAppearance.ForIngredient("unknown").carbonation, Is.Zero);
    }

    [Test]
    public void IngredientTuningDoesNotMutateTheSharedPhysicsAsset()
    {
        var original = new SphConfig();
        float viscosity = original.viscosity;
        var syrup = original.WithViscosity(LiquidAppearance.ForIngredient("grenadine").viscosityScale);
        Assert.That(original.viscosity, Is.EqualTo(viscosity));
        Assert.That(syrup.viscosity, Is.GreaterThan(viscosity));
        Assert.That(syrup.spacing, Is.EqualTo(original.spacing));
        Assert.That(syrup.densityIterations, Is.EqualTo(original.densityIterations));
    }

    [Test]
    public void FloorImpactProducesOneVisualImpulseWithoutChangingTheBoundary()
    {
        root = new GameObject("impact test");
        var particleObject = new GameObject("particle");
        particleObject.transform.SetParent(root.transform,false);
        var p = particleObject.AddComponent<SphParticle>();
        var config = new SphConfig { gravity = 9, densityIterations = 0 };
        p.Init(config);
        p.Activate(new Vector2(0,-0.9f));
        p.velocity = Vector2.down * 4;
        var sim = new SphSimulation(config,Vector2.one*-2,Vector2.one*2,
            particle => SphContainerBoundary.Constrain(particle,root.transform,Vector2.one,0.05f));
        sim.SynchronizeTransforms = false;
        sim.Register(p);
        for (int i=0;i<5;i++) sim.Tick(1f/120);
        Assert.That(Get<Vector4>(p,"renderImpact").z, Is.GreaterThan(0));
        Assert.That(p.pos.y, Is.GreaterThanOrEqualTo(-0.95001f));
        Set(p,"renderImpact",Vector4.zero);
        for (int i=0;i<30;i++) sim.Tick(1f/120);
        Assert.That(Get<Vector4>(p,"renderImpact").z, Is.Zero, "Resting gravity must not keep making beer foam.");
        p.Activate(Vector2.zero);
        Assert.That(Get<Vector4>(p,"renderImpact"), Is.EqualTo(Vector4.zero));
    }

    [Test]
    public void LiquidShadersCompileForTheActiveGraphicsDevice()
    {
        RequireGpu();
        foreach (string name in new[] { "LiquidBlobSplat.shader", "LiquidComposite.shader" })
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder+name);
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True, name);
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                Assert.That(message.severity.ToString(), Is.Not.EqualTo("Error"), message.message);
        }
    }

    [Test]
    public void GpuFieldKeepsWorldPositionsAndExcludesDetachedDropsFromSurface()
    {
        Setup("gin");
        Add(new Vector2(0,0.6f));
        Render(1f/60);
        var field = Get<RenderTexture>(renderer,"fieldTexture");
        Assert.That(Read(field,0.5f,0.8f).r, Is.GreaterThan(0.2f), "Upper particle must render in upper UV.");
        Assert.That(Read(field,0.5f,0.2f).r, Is.LessThan(0.01f), "Field must not be vertically flipped.");
        Assert.That(Read(Get<RenderTexture>(renderer,"surfaceRead"),0.5f,0.5f).a, Is.LessThan(0.1f));
        particles.Clear();
        Fill();
        Render(1f/60);
        var surface = Read(Get<RenderTexture>(renderer,"surfaceRead"),0.5f,0.5f);
        Assert.That(surface.a, Is.GreaterThan(0.9f));
        Assert.That(surface.r, Is.InRange(0.15f,0.65f));
        for (int y=0;y<14;y++) Add(new Vector2(0,-0.14f+y*0.065f));
        Render(1f/60);
        var underStream = Read(Get<RenderTexture>(renderer,"surfaceRead"),0.5f,0.5f);
        Assert.That(underStream.r, Is.LessThan(0.65f), "A narrow stream touching the liquid must not pull its foam up to the rim.");
    }

    [Test]
    public void ImpactCloudRisesFadesAndLeavesLongerLivedBeerFoam()
    {
        Setup("beer");
        Fill();
        foreach (var p in particles) Set(p,"renderImpact",new Vector4(p.pos.x,p.pos.y,0.7f,0));
        Render(1f/60);
        float initial = MeanRed(Get<RenderTexture>(renderer,"effectRead"));
        Assert.That(initial, Is.GreaterThan(0.01f));
        Capture("beer-impact");
        for (int i=0;i<90;i++) Render(1f/60);
        float settled = MeanRed(Get<RenderTexture>(renderer,"effectRead"));
        Assert.That(settled, Is.LessThan(initial * 0.75f));
        Assert.That(Read(Get<RenderTexture>(renderer,"surfaceRead"),0.5f,0.5f).g, Is.GreaterThan(0.01f));
        Capture("beer-settled");
    }

    [TestCase("gin")]
    [TestCase("orange_juice")]
    [TestCase("milk")]
    public void NonCarbonatedLiquidDoesNotCreateRisingCloudEvenWithStrongImpacts(string ingredient)
    {
        Setup(ingredient);
        Fill();
        var appearance = profile.ResolveAppearance(ingredient);
        appearance.impactCloud = 2f; // 오래된 인스펙터 override가 남아 있어도 비탄산이면 차단한다.
        renderer.Init(profile,Get<Color>(renderer,"liquidColor"),appearance);
        for (int frame=0;frame<10;frame++)
        {
            foreach (var p in particles) Set(p,"renderImpact",new Vector4(p.pos.x,p.pos.y,1f,0));
            Render(1f/60);
        }
        Assert.That(MeanRed(Get<RenderTexture>(renderer,"effectRead")),Is.Zero);
        Capture(ingredient+"-impact");
    }

    [TestCase("gin")]
    [TestCase("soda_water")]
    [TestCase("orange_juice")]
    public void MaterialPreviewAndDisableReenableDoNotKeepOldLiquid(string ingredient)
    {
        Setup(ingredient);
        Fill();
        for (int i=0;i<8;i++) Render(1f/60);
        Capture(ingredient);
        renderer.enabled = false;
        Invoke(renderer,"OnDisable");
        Assert.That(Get<RenderTexture>(renderer,"fieldTexture"), Is.Null);
        Assert.That(Get<GraphicsBuffer>(renderer,"particleBuffer"), Is.Null);
        renderer.enabled = true;
        Invoke(renderer,"OnEnable");
        particles.Clear();
        Render(1f/60);
        Assert.That(MeanRed(Get<RenderTexture>(renderer,"fieldTexture")), Is.LessThan(0.0001f));
        Assert.That(MeanRed(Get<RenderTexture>(renderer,"effectRead")), Is.LessThan(0.0001f));
    }

    [Test]
    public void MovingCameraAndGlassTogetherPreservesLocalCloud()
    {
        Setup("beer");
        Fill();
        foreach (var p in particles) Set(p,"renderImpact",new Vector4(p.pos.x,p.pos.y,0.6f,0));
        Render(1f/60);
        Color before = Read(Get<RenderTexture>(renderer,"effectRead"),0.5f,0.2f);
        Vector3 move = new Vector3(700,600,0);
        root.transform.position += move;
        foreach (var p in particles) p.pos += (Vector2)move;
        Render(0);
        Color after = Read(Get<RenderTexture>(renderer,"effectRead"),0.5f,0.2f);
        Assert.That(after.r, Is.EqualTo(before.r).Within(0.02f));
    }

    [Test]
    public void SaturationStaysInGamutAndKeepsLuminanceAndGreys()
    {
        foreach (var source in new[] {
            new Color(202/255f,162/255f,4/255f,0.75f), new Color(253/255f,180/255f,60/255f,0.75f),
            new Color(60/255f,30/255f,20/255f,0.75f), new Color(0f,145/255f,200/255f,0.75f),
            new Color(0.4f,0.6f,0.5f,0.35f) })
        {
            var boosted = LiquidAppearance.Saturate(source,1.6f);
            foreach (float channel in new[] { boosted.r,boosted.g,boosted.b })
                Assert.That(channel,Is.InRange(-0.0001f,1.0001f),"Saturation must not clip channels.");
            Assert.That(Luminance(boosted),Is.EqualTo(Luminance(source)).Within(0.001f));
            Assert.That(boosted.a,Is.EqualTo(source.a),"Data alpha stays separate from the visual correction.");
            // 채널 순서(색상)가 유지되어야 한다.
            Assert.That(Mathf.Sign(boosted.r-boosted.g),Is.EqualTo(Mathf.Sign(source.r-source.g)));
            Assert.That(Mathf.Sign(boosted.g-boosted.b),Is.EqualTo(Mathf.Sign(source.g-source.b)));
        }
        var grey = new Color(0.94f,0.94f,0.94f,0.35f);
        Assert.That(LiquidAppearance.Saturate(grey,1.5f),Is.EqualTo(grey));
        var orange = new Color(253/255f,180/255f,60/255f);
        Assert.That(Chroma(LiquidAppearance.Saturate(orange,LiquidAppearance.ForIngredient("orange_juice").saturation)),
            Is.GreaterThan(Chroma(orange)));
        Assert.That(LiquidAppearance.ForIngredient("gin").saturation,Is.LessThanOrEqualTo(1.1f));
    }

    [Test]
    public void AirborneWidthDoesNotMoveTheRestingSurface()
    {
        Setup("gin");
        FillBulk();
        profile.airborneWidth = 1f;
        Render(1f/60);
        float narrow = Read(Get<RenderTexture>(renderer,"surfaceRead"),0.5f,0.5f).r;
        profile.airborneWidth = 2.5f;
        Render(1f/60);
        float wide = Read(Get<RenderTexture>(renderer,"surfaceRead"),0.5f,0.5f).r;
        Assert.That(wide,Is.EqualTo(narrow).Within(0.002f),"Only flowing liquid is drawn wider.");
        // 떨어지는 방울은 두껍게 그려 한 픽셀 점이 되지 않게 한다.
        particles.Clear();
        Add(new Vector2(0.3f,0.5f));
        particles[0].velocity = Vector2.down * 3f;
        profile.airborneWidth = 1f;
        Render(1f/60);
        float thin = CountAbove(Get<RenderTexture>(renderer,"fieldTexture"),profile.threshold);
        profile.airborneWidth = 2.5f;
        Render(1f/60);
        float thick = CountAbove(Get<RenderTexture>(renderer,"fieldTexture"),profile.threshold);
        Assert.That(thick,Is.GreaterThan(thin*2f));
    }

    [Test]
    public void FoamSurfaceBridgesSingleMissingColumnAndFollowsHeightGradually()
    {
        Setup("beer");
        FillBulk();
        Render(1f/60);
        var material = Get<Material>(renderer,"compositeMaterial");
        var raw = new Texture2D(128,1,TextureFormat.RGBAFloat,false,true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var old = new Texture2D(128,1,TextureFormat.RGBAFloat,false,true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var target = new RenderTexture(128,1,0,RenderTextureFormat.ARGBFloat);
        try
        {
            var next = new Color[128]; var previous = new Color[128];
            for (int x=0;x<128;x++) { next[x]=new Color(0.4f,0,0,1); previous[x]=new Color(0.3f,0.6f,1,1); }
            next[64]=Color.clear; // 빠른 유입 때문에 추출 단계가 수면 한 열을 놓친 경우.
            raw.SetPixels(next); raw.Apply(); old.SetPixels(previous); old.Apply();
            material.SetTexture("_RawSurface",raw); material.SetTexture("_PreviousSurface",old);
            Graphics.Blit(Texture2D.blackTexture,target,material,3);
            var bridge=Read(target,64.5f/128,0.5f);
            Assert.That(bridge.a,Is.GreaterThan(0.9f),"A single missing column between valid surfaces must not split the head.");
            Assert.That(bridge.r,Is.InRange(0.301f,0.34f),"Head height must approach the new surface instead of jumping in one frame.");
            material.SetVector("_Step",new Vector4(0,1,profile.physics.spacing,0));
            Graphics.Blit(Texture2D.blackTexture,target,material,3);
            Assert.That(Read(target,0.3f,0.5f).r,Is.EqualTo(0.3f).Within(0.001f),"Paused foam height must stay unchanged.");
        }
        finally
        {
            Object.DestroyImmediate(raw); Object.DestroyImmediate(old);
            if (RenderTexture.active == target) RenderTexture.active = null;
            target.Release(); Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void DisplayedFoamDoesNotTurnNarrowSurfaceColumnsIntoTallTeeth()
    {
        Setup("beer");
        FillBulk(); AddStream(0.03f,-3.2f);
        for (int i=0;i<90;i++) Render(1f/60);
        var material=Get<Material>(renderer,"compositeMaterial");
        var synthetic=new Texture2D(128,1,TextureFormat.RGBAFloat,false,true) { filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp };
        var target=new RenderTexture(512,512,0,RenderTextureFormat.ARGB32);
        try
        {
            var columns=new Color[128];
            for(int x=0;x<128;x++) columns[x]=new Color(0.4f+(x%4==0?0.07f:0),0.7f,1,1);
            synthetic.SetPixels(columns);synthetic.Apply();
            material.SetTexture("_SurfaceTex",synthetic);
            Graphics.Blit(Texture2D.blackTexture,target);
            Graphics.Blit(Texture2D.blackTexture,target,material,0);
            Save(target,"foam-column-review");
            var picture=Copy(target);
            int last=-1,maxJump=0;
            for(int x=165;x<348;x++)
            {
                int top=-1;
                for(int y=0;y<512;y++)
                {
                    var c=picture.GetPixel(x,y);
                    if(c.r>0.65f && c.g>0.65f && c.b>0.5f) top=y;
                }
                if(top>=0 && last>=0) maxJump=Mathf.Max(maxJump,Mathf.Abs(top-last));
                last=top;
            }
            Object.DestroyImmediate(picture);
            TestContext.WriteLine($"foam maximum neighboring display-column jump={maxJump}px");
            Assert.That(maxJump,Is.LessThanOrEqualTo(7),"One narrow input column must not produce a tall foam tooth.");
        }
        finally
        {
            Object.DestroyImmediate(synthetic);
            if (RenderTexture.active == target) RenderTexture.active = null;
            target.Release();Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void StrongInflowMakesMoreBeerFoamThatHoldsThenCollapses()
    {
        float strong = FoamAfterInflow(0.03f,-3.2f,out float held,out float collapsed);
        Assert.That(held,Is.GreaterThan(strong*0.8f),"Beer foam must stay for a while after the pour stops.");
        Assert.That(collapsed,Is.LessThan(strong*0.6f),"Beer foam must slowly settle afterwards.");
        Cleanup();
        float gentle = FoamAfterInflow(0.12f,-1.4f,out _,out _);
        TestContext.WriteLine($"strong={strong}, gentle={gentle}, held={held}, collapsed={collapsed}");
        Assert.That(strong,Is.GreaterThan(0.3f));
        Assert.That(strong,Is.GreaterThan(gentle*1.4f),"A stronger pour must make a richer head.");
    }

    [Test]
    public void FoamAndCloudCanBeTunedIndependently()
    {
        Setup("beer");
        var appearance = profile.ResolveAppearance("beer");
        appearance.impactCloud = 0f;
        appearance.cloudOpacity = 0f;
        renderer.Init(profile,Get<Color>(renderer,"liquidColor"),appearance);
        FillBulk();
        AddStream(0.03f,-3.2f);
        for (int i=0;i<45;i++) Render(1f/60);
        Assert.That(MeanFoam(),Is.GreaterThan(0.2f),"Weakening the cloud must not remove beer foam.");
        Assert.That(MeanRed(Get<RenderTexture>(renderer,"effectRead")),Is.LessThan(0.01f));
        Cleanup();
        Setup("beer");
        appearance = profile.ResolveAppearance("beer");
        appearance.foamGain = 0f;
        renderer.Init(profile,Get<Color>(renderer,"liquidColor"),appearance);
        FillBulk();
        foreach (var p in particles) Set(p,"renderImpact",new Vector4(p.pos.x,p.pos.y,0.8f,0));
        Render(1f/60);
        Assert.That(MeanRed(Get<RenderTexture>(renderer,"effectRead")),Is.GreaterThan(0.005f));
        Assert.That(MeanFoam(),Is.LessThan(0.01f));
    }

    [Test]
    public void ImpactCloudRisesSpreadsAndThinsInsteadOfCoveringTheGlass()
    {
        Setup("beer");
        FillBulk();
        foreach (var p in particles)
            if (p.pos.y < -0.55f && Mathf.Abs(p.pos.x) < 0.1f) Set(p,"renderImpact",new Vector4(p.pos.x,p.pos.y,1f,0));
        Render(1f/60);
        var start = CloudStats(Get<RenderTexture>(renderer,"effectRead"));
        Capture("beer-cloud-start");
        for (int i=0;i<40;i++) Render(1f/60);
        var later = CloudStats(Get<RenderTexture>(renderer,"effectRead"));
        Capture("beer-cloud-later");
        TestContext.WriteLine($"start={start}, later={later}");
        Assert.That(start.x,Is.GreaterThan(0.2f),"Impacts must still make a visible micro-bubble clump.");
        Assert.That(later.y,Is.GreaterThan(start.y+0.03f),"The clump must rise with buoyancy.");
        Assert.That(later.z,Is.GreaterThan(start.z),"The clump must spread out as it rises.");
        Assert.That(later.x,Is.LessThan(start.x*0.8f),"The clump must thin out.");
        Assert.That(start.w,Is.LessThan(0.35f),"A single impact must not cover most of the liquid.");
    }

    float FoamAfterInflow(float gap,float speed,out float held,out float collapsed)
    {
        Setup("beer");
        FillBulk();
        int bulk = particles.Count;
        AddStream(gap,speed);
        for (int i=0;i<90;i++) Render(1f/60);
        float poured = MeanFoam();
        particles.RemoveRange(bulk,particles.Count-bulk);
        for (int i=0;i<60;i++) Render(1f/60);
        held = MeanFoam();
        for (int i=0;i<900;i++) Render(1f/60);
        collapsed = MeanFoam();
        return poured;
    }

    void FillBulk()
    {
        for (int y=0;y<9;y++)
            for (int x=0;x<13;x++)
                Add(new Vector2((x-6)*0.065f,-0.66f+y*0.065f));
    }

    /// <summary>수면(약 -0.14) 위에서 내려오는 줄기. gap이 좁을수록 유량이 크다.</summary>
    void AddStream(float gap,float speed)
    {
        for (float y=-0.1f;y<0.7f;y+=gap)
            foreach (float x in new[] { -0.03f,0.03f })
            {
                Add(new Vector2(x,y));
                particles[particles.Count-1].velocity = new Vector2(0,speed);
            }
    }

    float MeanFoam()
    {
        var copy = Copy(Get<RenderTexture>(renderer,"surfaceRead"));
        float sum = 0, count = 0;
        foreach (var p in copy.GetPixels()) if (p.a > 0.5f) { sum += p.g; count++; }
        Object.DestroyImmediate(copy);
        return sum/Mathf.Max(1,count);
    }

    /// <summary>x: 최대 농도, y: 농도 가중 높이, z: 가로 표준편차, w: 0.1 이상인 면적 비율(액체 영역 기준).</summary>
    static Vector4 CloudStats(RenderTexture target)
    {
        var copy = Copy(target);
        float sum = 0, height = 0, mx = 0, mxx = 0, covered = 0, liquid = 0, peak = 0;
        for (int y=0;y<copy.height;y++)
            for (int x=0;x<copy.width;x++)
            {
                float v = copy.GetPixel(x,y).r;
                float u = (x+0.5f)/copy.width, h = (y+0.5f)/copy.height;
                sum += v; height += v*h; mx += v*u; mxx += v*u*u; peak = Mathf.Max(peak,v);
                if (h < 0.42f) liquid++;
                if (v > 0.1f) covered++;
            }
        Object.DestroyImmediate(copy);
        float w = Mathf.Max(0.0001f,sum);
        float meanX = mx/w;
        return new Vector4(peak,height/w,Mathf.Sqrt(Mathf.Max(0,mxx/w-meanX*meanX)),covered/Mathf.Max(1,liquid));
    }

    static float CountAbove(RenderTexture target,float threshold)
    {
        var copy = Copy(target); float count = 0;
        foreach (var p in copy.GetPixels()) if (p.r >= threshold) count++;
        Object.DestroyImmediate(copy); return count;
    }

    static float Luminance(Color c) => c.r*0.2126f + c.g*0.7152f + c.b*0.0722f;
    static float Chroma(Color c) => Mathf.Max(c.r,Mathf.Max(c.g,c.b)) - Mathf.Min(c.r,Mathf.Min(c.g,c.b));

    void Setup(string ingredient)
    {
        RequireGpu();
        root = new GameObject("liquid gpu test");
        var camObject = new GameObject("camera");
        camObject.transform.SetParent(root.transform,false);
        camera = camObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 1;
        camera.transform.localPosition = new Vector3(0,0,-10);
        camera.pixelRect = new Rect(0,0,512,512);
        var glass = new GameObject("glass");
        glass.transform.SetParent(root.transform,false);
        var liquid = new GameObject("liquid");
        liquid.transform.SetParent(root.transform,false);
        renderer = liquid.AddComponent<SphLiquidRenderer>();
        profile = ScriptableObject.CreateInstance<LiquidProfile>();
        profile.physics.spacing = 0.065f;
        var appearance = profile.ResolveAppearance(ingredient);
        Color color = ingredient == "beer" ? new Color(0.792f,0.635f,0.016f) :
            ingredient == "orange_juice" ? new Color(0.992f,0.706f,0.235f) : new Color(0.94f,0.97f,1f);
        color.a = appearance.defaultAlpha;
        renderer.SetTargetCamera(camera);
        renderer.SetContainer(glass.transform,new Vector2(0.45f,0.7f));
        renderer.Init(profile,color,appearance);
    }

    void Add(Vector2 pos)
    {
        var go = new GameObject("particle");
        go.transform.SetParent(root.transform,false);
        var p = go.AddComponent<SphParticle>();
        p.Init(profile.physics);
        p.Activate(pos);
        particles.Add(p);
    }

    void Fill()
    {
        for (int y=0;y<9;y++)
            for (int x=0;x<13;x++)
                Add(new Vector2((x-6)*0.065f,-0.66f+y*0.065f));
        // 잔 위에서 들어오는 자유 물줄기도 캡처에 포함한다.
        for (int y=0;y<10;y++)
        {
            Add(new Vector2(-0.2f,0.1f+y*0.075f));
            particles[particles.Count-1].velocity = new Vector2(0,-0.6f);
        }
    }

    void Render(float dt)
    {
        renderer.UpdateBlobs(particles);
        var command = (CommandBuffer)typeof(SphLiquidRenderer).GetMethod("BuildCommands",BindingFlags.Instance|BindingFlags.NonPublic)
            .Invoke(renderer,new object[] { camera,dt });
        Graphics.ExecuteCommandBuffer(command);
    }

    void Capture(string name)
    {
        var previous = RenderTexture.active;
        var target = new RenderTexture(512,512,0,RenderTextureFormat.ARGB32);
        target.Create();
        var background = new Texture2D(32,32);
        var pixels = new Color[1024];
        for (int y=0;y<32;y++)
            for (int x=0;x<32;x++)
                pixels[y*32+x] = ((x/4+y/4)%2==0) ? new Color(0.16f,0.20f,0.24f) : new Color(0.21f,0.26f,0.29f);
        background.SetPixels(pixels); background.Apply();
        Graphics.Blit(background,target);
        Graphics.Blit(Texture2D.blackTexture,target,Get<Material>(renderer,"compositeMaterial"),0);
        Save(target,name);
        Object.DestroyImmediate(background);
        RenderTexture.active = previous;
        target.Release(); Object.DestroyImmediate(target);
    }

    static void Save(RenderTexture target,string name)
    {
        var copy = Copy(target);
        string directory = Path.Combine(Path.GetTempPath(),"PourLiquidValidation");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory,name+".png"),copy.EncodeToPNG());
        Object.DestroyImmediate(copy);
    }

    static void RequireGpu()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires a graphics device, run without -nographics.");
    }

    static Texture2D Copy(RenderTexture target)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var copy = new Texture2D(target.width,target.height,TextureFormat.RGBAFloat,false,true);
        copy.ReadPixels(new Rect(0,0,target.width,target.height),0,0);
        copy.Apply();
        RenderTexture.active = previous;
        return copy;
    }
    static Color Read(RenderTexture target,float u,float v)
    {
        var copy = Copy(target);
        Color color = copy.GetPixel(Mathf.Clamp((int)(u*target.width),0,target.width-1),Mathf.Clamp((int)(v*target.height),0,target.height-1));
        Object.DestroyImmediate(copy);
        return color;
    }
    static float MeanRed(RenderTexture target)
    {
        var copy = Copy(target); float sum = 0;
        foreach (var p in copy.GetPixels()) sum += p.r;
        float mean = sum/(copy.width*copy.height);
        Object.DestroyImmediate(copy); return mean;
    }
    static T Get<T>(object owner,string name) => (T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(owner);
    static void Set(object owner,string name,object value) => owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).SetValue(owner,value);
    static void Invoke(object owner,string name) => owner.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,null);
}
