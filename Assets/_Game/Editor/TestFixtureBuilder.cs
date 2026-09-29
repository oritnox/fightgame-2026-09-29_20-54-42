// /Assets/_Game/Editor/TestFixtureBuilder.cs
// 공용코드 수정: F140 P01 입력 시험용 메모리 fixture. 제품 에셋을 생성·저장하지 않음.
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RP.Editor
{
    public static class TestFixtureBuilder
    {
        public static InputActionAsset CreateP01InputAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "RP_P01_InMemory_Fixture";
            InputActionMap map = asset.AddActionMap("Combat");
            map.AddAction("LightAttack", InputActionType.Button);
            map.AddAction("HeavyAttack", InputActionType.Button);
            map.AddAction("Parry", InputActionType.Button);
            map.AddAction("Dodge", InputActionType.Button);
            map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            map.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            return asset;
        }

        public static InputAction RequireAction(InputActionAsset asset, string mapName, string actionName)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (string.IsNullOrWhiteSpace(mapName) || string.IsNullOrWhiteSpace(actionName))
                throw new ArgumentException("Map and action names are required.");
            InputActionMap map = asset.FindActionMap(mapName, throwIfNotFound: true);
            return map.FindAction(actionName, throwIfNotFound: true);
        }
    }
}
