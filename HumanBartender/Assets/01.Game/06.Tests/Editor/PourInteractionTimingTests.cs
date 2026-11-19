using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PourInteractionTimingTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    GameObject root;
    BottleTiltController bottle;
    PourManager manager;
    GimmickRunner runner;
    CraftTimer timer;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Pour interaction timing");
        root.SetActive(false);
        bottle = root.AddComponent<BottleTiltController>();
        bottle.ConfigurePourGeometry(Vector3.up, Vector3.up);
        manager = root.AddComponent<PourManager>();
        Set(manager, "bottle", bottle);
        Set(manager, "isPlay", true);
        runner = root.AddComponent<GimmickRunner>();
        timer = new CraftTimer();
        timer.Start();
        Set(runner, "runningTimer", timer);
        Set(runner, "currentGimmick", manager);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static void Call(object target, string name, float deltaTime) => target.GetType().GetMethod(name, Private).Invoke(target, new object[] { deltaTime });
    void TickTimer(float seconds) => Call(runner, "TickTimer", seconds);
    void TickBottle(float seconds) => Call(bottle, "Tick", seconds);
    void PickUp() => bottle.BeginDrag(bottle.PourPivotWorldPosition + Vector3.left);

    [Test]
    public void FirstInputRemainsAvailableWhileTimerAndFinishWaitForManipulation()
    {
        TickTimer(10);
        Assert.That(manager.IsManualInputActive, Is.True, "첫 조작을 받을 입력 경로는 열려 있어야 한다.");
        Assert.That(timer.ElapsedSec, Is.Zero);
        Assert.That(manager.IsTimeCounting, Is.False);
        Assert.That(manager.CanEndNow, Is.False);
        manager.EndNow();
        Assert.That(manager.IsManualInputActive, Is.True, "비활성 버튼의 종료 호출을 무시한다.");
    }

    [Test]
    public void ClockContinuesDuringReturnAndIdleUntilFinishIsAccepted()
    {
        PickUp();
        TickBottle(.3f);
        TickTimer(2);
        Assert.That(manager.CanEndNow, Is.False, "병을 들고 있으면 끝낼 수 없다.");
        manager.EndNow();
        Assert.That(manager.IsManualInputActive, Is.True);
        bottle.EndDrag();
        TickBottle(.05f);
        TickTimer(3);
        Assert.That(manager.CanEndNow, Is.False, "마우스를 놓아도 복귀가 끝나기 전에는 비활성이다.");
        Assert.That(timer.ElapsedSec, Is.EqualTo(5));
        TickBottle(1);
        Assert.That(manager.CanEndNow, Is.True);
        TickTimer(4);
        Assert.That(timer.ElapsedSec, Is.EqualTo(9), "병을 내려놓고 기다리는 동안에도 센다.");
        PickUp();
        Assert.That(manager.CanEndNow, Is.False, "다시 잡으면 즉시 비활성화한다.");
        bottle.EndDrag();
        TickBottle(1);
        manager.EndNow();
        TickTimer(10);
        Assert.That(timer.ElapsedSec, Is.EqualTo(9), "종료 후 액체 정리 시간은 제외한다.");
        Assert.That(manager.CanEndNow, Is.False);
    }

    [Test]
    public void FinishWaitsForRotationEvenWhenTheReturnMovementAlreadyEnded()
    {
        PickUp();
        TickBottle(.3f);
        Set(bottle, "putDownSpeed", 100f);
        Set(bottle, "returnSpeed", 10f);
        bottle.EndDrag();
        TickBottle(.1f);
        Assert.That(bottle.CurrentAngle, Is.GreaterThan(0));
        Assert.That(manager.CanEndNow, Is.False);
        TickBottle(20);
        Assert.That(bottle.CurrentAngle, Is.Zero);
        Assert.That(manager.CanEndNow, Is.True);
    }

    sealed class OtherGimmick : ICraftGimmick
    {
        public bool IsManualInputActive => true;
        public UniTask<GimmickResult> PlayAsync(GimmickStep step, CraftContext context, CraftTimer clock, CancellationToken token)
            => UniTask.FromResult<GimmickResult>(null);
    }

    [Test]
    public void GimmicksWithoutAFirstInputGateKeepTheirExistingTiming()
    {
        Set(runner, "currentGimmick", new OtherGimmick());
        TickTimer(3);
        Assert.That(timer.ElapsedSec, Is.EqualTo(3));
    }
}
