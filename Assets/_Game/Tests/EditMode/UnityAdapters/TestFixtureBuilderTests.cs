// /Assets/_Game/Tests/EditMode/UnityAdapters/TestFixtureBuilderTests.cs
// 공용코드 수정: F140 메모리 fixture가 프로젝트 asset을 생성하지 않는지 기본 구조 검증.
using NUnit.Framework;
using RP.Editor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RP.Tests.UnityAdapters
{
    public sealed class TestFixtureBuilderTests
    {
        [Test]
        public void CreatesExpectedInMemoryCombatActions()
        {
            InputActionAsset asset = TestFixtureBuilder.CreateP01InputAsset();
            try
            {
                Assert.That(asset.name, Is.EqualTo("RP_P01_InMemory_Fixture"));
                foreach (string name in new[] { "LightAttack", "HeavyAttack", "Parry", "Dodge", "Move", "Look" })
                    Assert.That(TestFixtureBuilder.RequireAction(asset, "Combat", name), Is.Not.Null);
                Assert.That(TestFixtureBuilder.RequireAction(asset, "Combat", "Move").expectedControlType, Is.EqualTo("Vector2"));
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void MissingActionIsNotSilentlyCreated()
        {
            InputActionAsset asset = TestFixtureBuilder.CreateP01InputAsset();
            try { Assert.Throws<System.ArgumentException>(() => TestFixtureBuilder.RequireAction(asset, "Combat", "Missing")); }
            finally { Object.DestroyImmediate(asset); }
        }
    }
}
