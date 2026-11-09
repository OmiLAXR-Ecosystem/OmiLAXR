/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System.Linq;
using UnityEngine;

namespace OmiLAXR
{
    /// <summary>
    /// Represents a collection of actors that can be treated as a single entity.
    /// Allows multiple actors to be grouped together and managed collectively.
    /// </summary>
    [AddComponentMenu("OmiLAXR / Actors / Actor Group")]
    [RequireComponent(typeof(Actor))]
    public class ActorGroup : Actor
    {
        /// <summary>
        /// Internal storage for the group members.
        /// </summary>
        private Actor[] _members;

        /// <summary>
        /// Returns the current array of actors that belong to this group.
        /// </summary>
        /// <returns>An array of Actor components that are members of this group.</returns>
        public Actor[] GetMembers()
        {
            // Members are collected in Start(); statements composed before that would otherwise get null
            if (_members == null)
                UpdateMemberList();
            return _members;
        }

        /// <summary>
        /// Initializes the group by collecting all member actors.
        /// </summary>
        private void Start()
        {
            UpdateMemberList();
        }

        /// <summary>
        /// Refreshes the internal list of group members by finding all Actor components
        /// attached to this GameObject, excluding the ActorGroup component itself.
        /// </summary>
        public void UpdateMemberList()
        {
            _members = GetComponents<Actor>()
                .Where(a => a.GetType() != typeof(ActorGroup)).ToArray();
        }

        /// <summary>
        /// Indicates that this actor represents a group rather than an individual.
        /// </summary>
        public override bool IsGroupActor => true;

        /// <summary>
        /// Sets default values for the group when the component is first added
        /// or reset in the Unity Inspector.
        /// </summary>
        private void Reset()
        {
            actorName = "Group";
            actorEmail = "group@omilaxr.dev";
        }
    }
}