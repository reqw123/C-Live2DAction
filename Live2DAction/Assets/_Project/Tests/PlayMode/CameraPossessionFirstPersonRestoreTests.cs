using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.CameraSystem;
using Live2DAction.Input;
using Object = UnityEngine.Object;

// 2026-08-29, user report ("有時用 C 切到貓，玩家消失了只剩一把大劍裝飾品"). First person hides the
// player's Visual renderers every LateUpdate (ThirdPersonCameraController.SetOwnVisualHidden),
// keeping only firstPersonVisibleWeapon shown. CameraPossessionSwitcher's C swap SetActive-disables
// the player camera, so that LateUpdate stops and the hidden renderers were never restored - the
// player stayed invisible (just the sword) for as long as you were the cat. The fix restores them
// in ThirdPersonCameraController.OnDisable; this exercises the whole swap under the real Play loop
// (aiming / SetOwnVisualHidden are both gated on Application.isPlaying, so EditMode can't reach it).
public class CameraPossessionFirstPersonRestoreTests
{
    private class StubInput : MonoBehaviour, IInputCommand
    {
        public Vector2 MoveInput => Vector2.zero;
        public bool AttackPressed => false;
        public bool DodgePressed => false;
        public bool LockOnPressed => false;
        public bool JumpPressed => false;
        public bool UltimatePressed => false;
        public bool FlyPressed => false;
        public bool FlyDescendPressed => false;
        public bool BoostPressed => false;
        public bool AimPressed => false;
        public bool FirePressed => false;
        public bool ViewTogglePressed => false;
        public bool ZoomInPressed => false;
        public bool ZoomOutPressed => false;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    private GameObject _player;
    private GameObject _playerCam;
    private GameObject _catCam;
    private GameObject _switcherGo;
    private MeshRenderer _bodyRenderer;
    private MeshRenderer _weaponRenderer;

    [SetUp]
    public void SetUp()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }

        _player = new GameObject("Player");
        _player.transform.position = Vector3.zero;

        var visual = new GameObject("Visual");
        visual.transform.SetParent(_player.transform);
        visual.transform.localPosition = Vector3.zero;

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(visual.transform);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        _bodyRenderer = body.GetComponent<MeshRenderer>();

        var weapon = GameObject.CreatePrimitive(PrimitiveType.Cube);
        weapon.name = "Weapon"; // the "大劍" - firstPersonVisibleWeapon keeps this one shown
        weapon.transform.SetParent(visual.transform);
        Object.DestroyImmediate(weapon.GetComponent<Collider>());
        _weaponRenderer = weapon.GetComponent<MeshRenderer>();

        _playerCam = new GameObject("PlayerCam");
        var tpc = _playerCam.AddComponent<ThirdPersonCameraController>();
        SetField(tpc, "target", _player.transform);
        SetField(tpc, "inputSource", _player.AddComponent<StubInput>());
        SetField(tpc, "firstPersonVisibleWeapon", weapon.transform);
        SetField(tpc, "initialYaw", 0f);
        SetField(tpc, "initialPitch", 0f);
        SetField(tpc, "_viewToggledFirstPerson", true); // as if the player had pressed V

        _catCam = new GameObject("CatCam");
        _catCam.SetActive(false);

        _switcherGo = new GameObject("CameraPossession");
        var switcher = _switcherGo.AddComponent<CameraPossessionSwitcher>();
        const BindingFlags priv = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(CameraPossessionSwitcher).GetField("playerCamera", priv).SetValue(switcher, _playerCam);
        typeof(CameraPossessionSwitcher).GetField("catCamera", priv).SetValue(switcher, _catCam);
        typeof(CameraPossessionSwitcher).GetField("playerControl", priv).SetValue(switcher, new Behaviour[0]);
        typeof(CameraPossessionSwitcher).GetField("catControl", priv).SetValue(switcher, new Behaviour[0]);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_switcherGo);
        Object.DestroyImmediate(_catCam);
        Object.DestroyImmediate(_playerCam);
        Object.DestroyImmediate(_player);
    }

    [UnityTest]
    public IEnumerator SwappingToCatWhileFirstPerson_LeavesThePlayerBodyVisible()
    {
        // Let the player camera's LateUpdate run - first person hides the body, keeps the weapon.
        yield return null;
        yield return null;

        Assert.IsFalse(_bodyRenderer.enabled,
            "Test precondition: first person should have hidden the player's body renderer");
        Assert.IsTrue(_weaponRenderer.enabled,
            "Test precondition: firstPersonVisibleWeapon (the sword) stays shown in first person");

        var switcher = _switcherGo.GetComponent<CameraPossessionSwitcher>();
        switcher.FocusCat();
        yield return null;

        Assert.IsFalse(_playerCam.activeSelf, "Test precondition: the player camera is switched off");
        Assert.IsTrue(_bodyRenderer.enabled,
            "Swapping to the cat must re-enable the body renderers first person hid - otherwise the " +
            "player is invisible (just the sword) for as long as you're the cat");
    }

    [UnityTest]
    public IEnumerator SwappingBackToPlayer_StillFirstPerson_HidesTheBodyAgain()
    {
        yield return null;
        yield return null;
        var switcher = _switcherGo.GetComponent<CameraPossessionSwitcher>();

        switcher.FocusCat();
        yield return null;
        Assert.IsTrue(_bodyRenderer.enabled, "Test precondition: body restored on the swap away");

        switcher.FocusPlayer();
        yield return null;
        yield return null;

        // The V-toggle is still on, so returning to the player resumes first person and re-hides
        // the body - correct and self-consistent (you're looking through the player's eyes again).
        Assert.IsFalse(_bodyRenderer.enabled,
            "Back on the player in first person, the body should hide again");
    }
}
