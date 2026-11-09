using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OmiLAXR.Tests.Actors
{
    public class ActorGroup_Tests
    {
        private GameObject _gameObject;
        private ActorGroup _actorGroup;
        
        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("TestActorGroup");
            _actorGroup = _gameObject.AddComponent<ActorGroup>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_actorGroup.gameObject);
        }

        [Test]
        public void ActorGroup_Tests_Has_Default_Values()
        {
            // Assert (the "Group" defaults are only applied by Reset() when added in the inspector)
            Assert.IsTrue(_actorGroup.IsGroupActor);
            Assert.IsEmpty(_actorGroup.GetMembers());
        }

        [Test]
        public void ActorGroup_Tests_Can_Set_Members()
        {
            // Arrange
            var member1 = _actorGroup.gameObject.AddComponent<Actor>();
            var member2 = _actorGroup.gameObject.AddComponent<Actor>();
            
            var members = _actorGroup.GetMembers();
            Array.Sort(members, (actor, actor1) => string.CompareOrdinal(actor.actorName, actor1.actorName));

            // Act & Assert
            Assert.AreEqual(2, members.Length);
            Assert.Contains(member1, members);
            Assert.Contains(member2, members);
        }

        [Test]
        public void ActorGroup_Tests_Can_Set_ActorName_And_Email()
        {
            // Act
            _actorGroup.actorName = "Test Group Name";
            _actorGroup.actorEmail = "testgroup@example.com";

            // Assert
            Assert.AreEqual("Test Group Name", _actorGroup.actorName);
            Assert.AreEqual("testgroup@example.com", _actorGroup.actorEmail);
        }

        [Test]
        public void ActorGroup_Tests_Members_Array_Can_Be_Empty()
        {
            // Act
            var members = _actorGroup.GetMembers();

            // Assert
            Assert.IsNotNull(members);
            Assert.IsEmpty(members);
        }

        [Test]
        public void ActorGroup_Tests_ActorGroup_Inherits_Actor_Properties()
        {
            // Act
            _actorGroup.actorName = "Group Name";
            _actorGroup.actorEmail = "group@example.com";

            // Assert
            Assert.AreEqual("Group Name", _actorGroup.actorName);
            Assert.AreEqual("group@example.com", _actorGroup.actorEmail);
        }
    }
}