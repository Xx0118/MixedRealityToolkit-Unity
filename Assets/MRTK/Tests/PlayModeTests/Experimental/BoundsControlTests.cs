// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if !WINDOWS_UWP
// When the .NET scripting backend is enabled and C# projects are built
// The assembly that this file is part of is still built for the player,
// even though the assembly itself is marked as a test assembly (this is not
// expected because test assemblies should not be included in player builds).
// Because the .NET backend is deprecated in 2018 and removed in 2019 and this
// issue will likely persist for 2018, this issue is worked around by wrapping all
// play mode tests in this check.

using Assert = UnityEngine.Assertions.Assert;
using Microsoft.MixedReality.Toolkit.Experimental.UI.BoundsControl;
using Microsoft.MixedReality.Toolkit.Experimental.UI.BoundsControlTypes;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Utilities;
using NUnit.Framework;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Microsoft.MixedReality.Toolkit.Tests.Experimental
{
    /// <summary>
    /// Tests for runtime behavior of bounds control
    /// </summary>
    public class PlayModeBoundsControlTests
    {
        private Material testMaterial;
        private Material testMaterialGrabbed;

        #region Utilities
        [SetUp]
        public void Setup()
        {
            PlayModeTestUtilities.Setup();

            // create shared test materials
            var shader = StandardShaderUtility.MrtkStandardShader;
            testMaterial = new Material(shader);
            testMaterial.color = Color.yellow;

            testMaterialGrabbed = new Material(shader);
            testMaterialGrabbed.color = Color.green;
        }

        [TearDown]
        public void ShutdownMrtk()
        {
            PlayModeTestUtilities.TearDown();
        }

        private readonly Vector3 boundsControlStartCenter = Vector3.forward * 1.5f;
        private readonly Vector3 boundsControlStartScale = Vector3.one * 0.5f;

        // SDK/Features/UX/Prefabs/AppBar/AppBar.prefab
        private const string appBarPrefabGuid = "83c02591e2867124181bcd3bcb65e288";
        private static readonly string appBarPrefabLink = AssetDatabase.GUIDToAssetPath(appBarPrefabGuid);

        /// <summary>
        /// Instantiates a bounds control at boundsControlStartCenter
        /// transform is at scale boundsControlStartScale
        /// </summary>
        private BoundsControl InstantiateSceneAndDefaultBoundsControl()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = boundsControlStartCenter;
            cube.transform.localScale = boundsControlStartScale;
            BoundsControl boundsControl = cube.AddComponent<BoundsControl>();
            TestUtilities.PlayspaceToOriginLookingForward();
            boundsControl.Active = true;

            return boundsControl;
        }

        /// <summary>
        /// Tests if the initial transform setup of bounds control has been propagated to it's collider
        /// </summary>
        /// <param name="boundsControl">Bounds control that controls the collider size</param>
        private IEnumerator VerifyInitialBoundsCorrect(BoundsControl boundsControl)
        {
            yield return null;
            yield return new WaitForFixedUpdate();
            BoxCollider boxCollider = boundsControl.GetComponent<BoxCollider>();
            var bounds = boxCollider.bounds;
            TestUtilities.AssertAboutEqual(bounds.center, boundsControlStartCenter, "bounds control incorrect center at start");
            TestUtilities.AssertAboutEqual(bounds.size, boundsControlStartScale, "bounds control incorrect size at start");
            yield return null;
        }
        #endregion

        /// <summary>
        /// Verify that we can instantiate bounds control at runtime
        /// </summary>
        [UnityTest]
        public IEnumerator BoundsControlInstantiate()
        {
            BoundsControl boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);
            Assert.IsNotNull(boundsControl);

            GameObject.Destroy(boundsControl.gameObject);
            // Wait for a frame to give Unity a change to actually destroy the object
            yield return null;
        }

        /// <summary>
        /// Test that if we update the bounds of a box collider, that the corners will move correctly
        /// </summary>
        [UnityTest]
        public IEnumerator BoundsOverrideTest()
        {
            BoundsControl boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);
            boundsControl.BoundsControlActivation = BoundsControlActivationType.ActivateOnStart;
            boundsControl.HideElementsInInspector = false;
            yield return null;

            var newObject = new GameObject();
            var bc = newObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(.25f, 0, 0);
            bc.size = new Vector3(0.162f, 0.1f, 1);
            boundsControl.BoundsOverride = bc;
            yield return null;

            Bounds b = GetBoundsControlRigBounds(boundsControl);

            Debug.Assert(b.center == bc.center, $"bounds center should be {bc.center} but they are {b.center}");
            Debug.Assert(b.size == bc.size, $"bounds size should be {bc.size} but they are {b.size}");

            GameObject.Destroy(boundsControl.gameObject);
            GameObject.Destroy(newObject);
            // Wait for a frame to give Unity a change to actually destroy the object
            yield return null;
        }

        /// <summary>
        /// Test that if we toggle the bounding box's active status,
        /// that the size of the boundsOverride is consistent, even
        /// when BoxPadding is set.
        /// </summary>
        [UnityTest]
        public IEnumerator BoundsOverridePaddingReset()
        {
            BoundsControl boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);
            boundsControl.BoundsControlActivation = BoundsControlActivationType.ActivateOnStart;
            boundsControl.HideElementsInInspector = false;

            // Set the bounding box to have a large padding.
            boundsControl.BoxPadding = Vector3.one;
            yield return null;

            var newObject = new GameObject();
            var bc = newObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(1, 2, 3);
            var backupSize = bc.size = new Vector3(1, 2, 3);
            boundsControl.BoundsOverride = bc;
            yield return null;

            // Toggles the bounding box and verifies
            // integrity of the measurements.
            VerifyBoundingBox();

            // Change the center and size of the boundsOverride
            // in the middle of execution, to ensure
            // these changes will be correctly reflected
            // in the BoundingBox after toggling.
            bc.center = new Vector3(0.1776f, 0.42f, 0.0f);
            backupSize = bc.size = new Vector3(0.1776f, 0.42f, 1.0f);
            boundsControl.BoundsOverride = bc;
            yield return null;

            // Toggles the bounding box and verifies
            // integrity of the measurements.
            VerifyBoundingBox();

            // Private helper function to prevent code copypasta.
            IEnumerator VerifyBoundingBox()
            {
                // Toggle the bounding box active status to check that the boundsOverride
                // will persist, and will not be destructively resized 
                boundsControl.gameObject.SetActive(false);
                yield return null;
                Debug.Log($"bc.size = {bc.size}");
                boundsControl.gameObject.SetActive(true);
                yield return null;
                Debug.Log($"bc.size = {bc.size}");

                Bounds b = GetBoundsControlRigBounds(boundsControl);

                var expectedSize = backupSize + Vector3.Scale(boundsControl.BoxPadding, newObject.transform.lossyScale);
                Debug.Assert(b.center == bc.center, $"bounds center should be {bc.center} but they are {b.center}");
                Debug.Assert(b.size == expectedSize, $"bounds size should be {expectedSize} but they are {b.size}");
                Debug.Assert(bc.size == expectedSize, $"boundsOverride's size was corrupted.");
            }

            GameObject.Destroy(boundsControl.gameObject);
            GameObject.Destroy(newObject);
            // Wait for a frame to give Unity a change to actually destroy the object
            yield return null;
        }

        /// <summary>
        /// Uses near interaction to scale the bounds control by directly grabbing corner
        /// </summary>
        [UnityTest]
        public IEnumerator FlickeringBoundsTest()
        {
            BoundsControl boundsControl = InstantiateSceneAndDefaultBoundsControl();
            boundsControl.BoundsControlActivation = BoundsControlActivationType.ActivateByProximityAndPointer;
            yield return VerifyInitialBoundsCorrect(boundsControl);
            var inputSimulationService = PlayModeTestUtilities.GetInputSimulationService();
            
            boundsControl.gameObject.transform.position = new Vector3(0, 0, 1.386f);
            boundsControl.gameObject.transform.rotation = Quaternion.Euler(0, 45.0f, 0);
            
            TestHand hand = new TestHand(Handedness.Left);
            yield return hand.Show(new Vector3(0, 0, 1));
            
            yield return PlayModeTestUtilities.WaitForInputSystemUpdate();

            // Check for a few loops that the hand is not flickering between states
            // number of iterations is an arbirary number to check that the box isn't flickering
            int iterations = 15;
            for (int i = 0; i < iterations; i++)
            {
                Assert.IsFalse(hand.GetPointer<SpherePointer>().IsNearObject);
                yield return null;
            }
        }

        /// <summary>
        /// Uses near interaction to scale the bounds control by directly grabbing corner
        /// </summary>
        [UnityTest]
        public IEnumerator ScaleViaNearInteraction()
        {
            BoundsControl boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);
            var inputSimulationService = PlayModeTestUtilities.GetInputSimulationService();

            // front right corner is corner 3
            var frontRightCornerPos = boundsControl.gameObject.transform.Find("rigRoot/corner_3").position;


            Vector3 initialHandPosition = new Vector3(0, 0, 0.5f);
            // This particular test is sensitive to the number of test frames, and is run at a slower pace.
            int numSteps = 30;
            var delta = new Vector3(0.1f, 0.1f, 0f);
            yield return PlayModeTestUtilities.ShowHand(Handedness.Right, inputSimulationService, ArticulatedHandPose.GestureId.OpenSteadyGrabPoint, initialHandPosition);
            yield return PlayModeTestUtilities.MoveHand(initialHandPosition, frontRightCornerPos, ArticulatedHandPose.GestureId.OpenSteadyGrabPoint, Handedness.Right, inputSimulationService, numSteps);
            yield return PlayModeTestUtilities.MoveHand(frontRightCornerPos, frontRightCornerPos + delta, ArticulatedHandPose.GestureId.Pinch, Handedness.Right, inputSimulationService, numSteps);

            var endBounds = boundsControl.GetComponent<BoxCollider>().bounds;
            Vector3 expectedCenter = new Vector3(0.033f, 0.033f, 1.467f);
            Vector3 expectedSize = Vector3.one * .567f;
            TestUtilities.AssertAboutEqual(endBounds.center, expectedCenter, "endBounds incorrect center");
            TestUtilities.AssertAboutEqual(endBounds.size, expectedSize, "endBounds incorrect size");

            GameObject.Destroy(boundsControl.gameObject);
            // Wait for a frame to give Unity a change to actually destroy the object
            yield return null;
        }

        /// <summary>
        /// Test bounds control rotation via far interaction
        /// Verifies gameobject has rotation in one axis only applied and no other transform changes happen during interaction
        /// </summary>
        [UnityTest]
        public IEnumerator RotateViaFarInteraction()
        {
            BoundsControl boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);

            Vector3 pointOnCube = new Vector3(-0.033f, -0.129f, 0.499f); // position where hand ray points on center of the test cube
            Vector3 rightFrontRotationHandlePoint = new Vector3(0.121f, -0.127f, 0.499f); // position of hand for far interacting with front right rotation sphere 
            Vector3 endRotation = new Vector3(-0.18f, -0.109f, 0.504f); // end position for far interaction scaling

            TestHand hand = new TestHand(Handedness.Left);
            yield return hand.Show(pointOnCube); // Initially make sure that hand ray is pointed on cube surface so we won't go behind the cube with our ray
            // grab front right rotation point
            yield return hand.MoveTo(rightFrontRotationHandlePoint);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Pinch);
            // move to left side of cube
            yield return hand.MoveTo(endRotation);

            // make sure rotation is as expected and no other transform values have been modified through this
            Vector3 expectedPosition = new Vector3(0f, 0f, 1.5f);
            Vector3 expectedSize = Vector3.one * 0.5f;
            float angle;
            Vector3 axis = new Vector3();
            boundsControl.transform.rotation.ToAngleAxis(out angle, out axis);
            float expectedAngle = 85f;
            float angleDiff = Mathf.Abs(expectedAngle - angle);
            Vector3 expectedAxis = new Vector3(0f, 1f, 0f);
            TestUtilities.AssertAboutEqual(axis, expectedAxis, "Rotated around wrong axis");
            Assert.IsTrue(angleDiff <= 1f, "cube didn't rotate as expected");
            TestUtilities.AssertAboutEqual(boundsControl.transform.position, expectedPosition, "cube moved while rotating");
            TestUtilities.AssertAboutEqual(boundsControl.transform.localScale, expectedSize, "cube scaled while rotating");

            GameObject.Destroy(boundsControl.gameObject);
            // Wait for a frame to give Unity a change to actually destroy the object
            yield return null;
        }

        /// <summary>
        /// Test bounds control rotation via far interaction, while moving extremely slowly.
        /// Rotation amount should be coherent even with extremely small per-frame motion
        /// </summary>
        [UnityTest]
        public IEnumerator RotateVerySlowlyViaFarInteraction()
        {
            BoundsControl boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);

            Vector3 pointOnCube = new Vector3(-0.033f, -0.129f, 0.499f); // position where hand ray points on center of the test cube
            Vector3 rightFrontRotationHandlePoint = new Vector3(0.121f, -0.127f, 0.499f); // position of hand for far interacting with front right rotation sphere 
            Vector3 endRotation = new Vector3(-0.18f, -0.109f, 0.504f); // end position for far interaction scaling

            TestHand hand = new TestHand(Handedness.Left);
            yield return hand.Show(pointOnCube); // Initially make sure that hand ray is pointed on cube surface so we won't go behind the cube with our ray
            // grab front right rotation point
            yield return hand.MoveTo(rightFrontRotationHandlePoint);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Pinch);

            // First, we make a series of very very tiny movements, as if the user
            // is making very precise adjustments to the rotation. If the rotation is
            // being calculated per-frame instead of per-manipulation-event, this should
            // induce drift/error.
            for (int i = 0; i < 50; i++)
            {
                yield return hand.MoveTo(Vector3.Lerp(rightFrontRotationHandlePoint, endRotation, (1/1000.0f) * i));
            }

            // Move the rest of the way very quickly.
            yield return hand.MoveTo(endRotation);

            // make sure rotation is as expected and no other transform values have been modified through this
            Vector3 expectedPosition = new Vector3(0f, 0f, 1.5f);
            Vector3 expectedSize = Vector3.one * 0.5f;
            float angle;
            Vector3 axis = new Vector3();
            boundsControl.transform.rotation.ToAngleAxis(out angle, out axis);
            float expectedAngle = 85f;
            float angleDiff = Mathf.Abs(expectedAngle - angle);
            Vector3 expectedAxis = new Vector3(0f, 1f, 0f);
            TestUtilities.AssertAboutEqual(axis, expectedAxis, "Rotated around wrong axis");
            Assert.IsTrue(angleDiff <= 1f, "cube didn't rotate as expected");
            TestUtilities.AssertAboutEqual(boundsControl.transform.position, expectedPosition, "cube moved while rotating");
            TestUtilities.AssertAboutEqual(boundsControl.transform.localScale, expectedSize, "cube scaled while rotating");

            GameObject.Destroy(boundsControl.gameObject);
            // Wait for a frame to give Unity a change to actually destroy the object
            yield return null;
        }

        /// <summary>
        /// Test bounds control rotation via near interaction
        /// Verifies gameobject has rotation in one axis only applied and no other transform changes happen during interaction
        /// </summary>
        [UnityTest]
        public IEnumerator RotateViaNearInteraction()
        {
            BoundsControl boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);

            Vector3 pointOnCube = new Vector3(-0.033f, -0.129f, 0.499f); // position where hand ray points on center of the test cube
            Vector3 rightFrontRotationHandlePoint = new Vector3(0.248f, 0.001f, 1.226f); // position of hand for far interacting with front right rotation sphere 
            Vector3 endRotation = new Vector3(-0.284f, -0.001f, 1.23f); // end position for far interaction scaling

            TestHand hand = new TestHand(Handedness.Left);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.OpenSteadyGrabPoint);
            yield return hand.Show(pointOnCube);
            // grab front right rotation point
            yield return hand.MoveTo(rightFrontRotationHandlePoint);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Pinch);
            // move to left side of cube
            yield return hand.MoveTo(endRotation);

            // make sure rotation is as expected and no other transform values have been modified through this
            Vector3 expectedPosition = new Vector3(0f, 0f, 1.5f);
            Vector3 expectedSize = Vector3.one * 0.5f;
            float angle;
            Vector3 axis = new Vector3();
            boundsControl.transform.rotation.ToAngleAxis(out angle, out axis);
            float expectedAngle = 90f;
            float angleDiff = Mathf.Abs(expectedAngle - angle);
            Vector3 expectedAxis = new Vector3(0f, 1f, 0f);
            TestUtilities.AssertAboutEqual(axis, expectedAxis, "Rotated around wrong axis");
            Assert.IsTrue(angleDiff <= 1f, "cube didn't rotate as expected");
            TestUtilities.AssertAboutEqual(boundsControl.transform.position, expectedPosition, "cube moved while rotating");
            TestUtilities.AssertAboutEqual(boundsControl.transform.localScale, expectedSize, "cube scaled while rotating");

            GameObject.Destroy(boundsControl.gameObject);
            // Wait for a frame to give Unity a change to actually destroy the object
            yield return null;
        }

        /// <summary>
        /// Test bounds control rotation via near interaction, while moving extremely slowly.
        /// Rotation amount should be coherent even with extremely small per-frame motion
        /// </summary>
        [UnityTest]
        public IEnumerator RotateVerySlowlyViaNearInteraction()
        {
            BoundsControl boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);

            Vector3 pointOnCube = new Vector3(-0.033f, -0.129f, 0.499f); // position where hand ray points on center of the test cube
            Vector3 rightFrontRotationHandlePoint = new Vector3(0.248f, 0.001f, 1.226f); // position of hand for far interacting with front right rotation sphere 
            Vector3 endRotation = new Vector3(-0.284f, -0.001f, 1.23f); // end position for far interaction scaling

            TestHand hand = new TestHand(Handedness.Left);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.OpenSteadyGrabPoint);
            yield return hand.Show(pointOnCube);
            // grab front right rotation point
            yield return hand.MoveTo(rightFrontRotationHandlePoint);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Pinch);
            
            // First, we make a series of very very tiny movements, as if the user
            // is making very precise adjustments to the rotation. If the rotation is
            // being calculated per-frame instead of per-manipulation-event, this should
            // induce drift/error.
            for (int i = 0; i < 50; i++)
            {
                yield return hand.MoveTo(Vector3.Lerp(rightFrontRotationHandlePoint, endRotation, (1/1000.0f) * i));
            }

            // Move the rest of the way very quickly.
            yield return hand.MoveTo(endRotation);

            // make sure rotation is as expected and no other transform values have been modified through this
            Vector3 expectedPosition = new Vector3(0f, 0f, 1.5f);
            Vector3 expectedSize = Vector3.one * 0.5f;
            float angle;
            Vector3 axis = new Vector3();
            boundsControl.transform.rotation.ToAngleAxis(out angle, out axis);
            float expectedAngle = 90f;
            float angleDiff = Mathf.Abs(expectedAngle - angle);
            Vector3 expectedAxis = new Vector3(0f, 1f, 0f);
            TestUtilities.AssertAboutEqual(axis, expectedAxis, "Rotated around wrong axis");
            Assert.IsTrue(angleDiff <= 1f, $"cube didn't rotate as expected, actual angle: {angle}");
            TestUtilities.AssertAboutEqual(boundsControl.transform.position, expectedPosition, "cube moved while rotating");
            TestUtilities.AssertAboutEqual(boundsControl.transform.localScale, expectedSize, "cube scaled while rotating");

            GameObject.Destroy(boundsControl.gameObject);
            // Wait for a frame to give Unity a change to actually destroy the object
            yield return null;
        }

        /// <summary>
        /// Test bounds control rotation via HoloLens 1 interaction / GGV
        /// Verifies gameobject has rotation in one axis only applied and no other transform changes happen during interaction
        /// </summary>
        [UnityTest]
        public IEnumerator RotateViaHololens1Interaction()
        {
            BoundsControl control = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(control);
            PlayModeTestUtilities.PushHandSimulationProfile();
            PlayModeTestUtilities.SetHandSimulationMode(HandSimulationMode.Gestures);

            // move camera to look at rotation sphere
            CameraCache.Main.transform.LookAt(new Vector3(0.248f, 0.001f, 1.226f)); // rotation sphere front right

            var startHandPos = new Vector3(0.364f, -0.157f, 0.437f);
            var endPoint = new Vector3(0.141f, -0.163f, 0.485f);

            // perform tab with hand and drag to left 
            TestHand rightHand = new TestHand(Handedness.Right);
            yield return rightHand.Show(startHandPos);
            yield return rightHand.SetGesture(ArticulatedHandPose.GestureId.Pinch);
            yield return rightHand.MoveTo(endPoint);

            // make sure only Y axis rotation was performed and no other transform values have changed
            Vector3 expectedPosition = new Vector3(0f, 0f, 1.5f);
            Vector3 expectedSize = Vector3.one * 0.5f;
            float angle;
            Vector3 axis = new Vector3();
            control.transform.rotation.ToAngleAxis(out angle, out axis);
            float expectedAngle = 85f;
            float angleDiff = Mathf.Abs(expectedAngle - angle);
            Vector3 expectedAxis = new Vector3(0f, 1f, 0f);
            TestUtilities.AssertAboutEqual(axis, expectedAxis, "Rotated around wrong axis");
            Assert.IsTrue(angleDiff <= 1f, "cube didn't rotate as expected");
            TestUtilities.AssertAboutEqual(control.transform.position, expectedPosition, "cube moved while rotating");
            TestUtilities.AssertAboutEqual(control.transform.localScale, expectedSize, "cube scaled while rotating");

            GameObject.Destroy(control.gameObject);
            // Wait for a frame to give Unity a change to actually destroy the object
            yield return null;

            // Restore the input simulation profile
            PlayModeTestUtilities.PopHandSimulationProfile();

            yield return null;
        }

        /// <summary>
        /// Tests scaling of bounds control by grabbing a corner with the far interaction hand ray
        /// </summary>
        [UnityTest]
        public IEnumerator ScaleViaFarInteraction()
        {
            BoundsControl boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);

            Vector3 rightCornerInteractionPoint = new Vector3(0.184f, 0.078f, 0.79f); // position of hand for far interacting with front right corner 
            Vector3 pointOnCube = new Vector3(-0.033f, -0.129f, 0.499f); // position where hand ray points on center of the test cube
            Vector3 scalePoint = new Vector3(0.165f, 0.267f, 0.794f); // end position for far interaction scaling

            TestHand hand = new TestHand(Handedness.Left);
            yield return hand.Show(pointOnCube); // Initially make sure that hand ray is pointed on cube surface so we won't go behind the cube with our ray
            yield return hand.MoveTo(rightCornerInteractionPoint);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Pinch);
            yield return hand.MoveTo(scalePoint);
            var endBounds = boundsControl.GetComponent<BoxCollider>().bounds;
            Vector3 expectedCenter = new Vector3(0.0453f, 0.0453f, 1.455f);
            Vector3 expectedSize = Vector3.one * 0.59f;
            TestUtilities.AssertAboutEqual(endBounds.center, expectedCenter, "endBounds incorrect center");
            TestUtilities.AssertAboutEqual(endBounds.size, expectedSize, "endBounds incorrect size");

            GameObject.Destroy(boundsControl.gameObject);
            // Wait for a frame to give Unity a change to actually destroy the object
            yield return null;
        }

        /// <summary>
        /// This tests the minimum and maximum scaling for the bounds control.
        /// </summary>
        [UnityTest]
        public IEnumerator ScaleMinMax()
        {
            float minScale = 0.5f;
            float maxScale = 2f;

            BoundsControl boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);
            var scaleHandler = boundsControl.EnsureComponent<MinMaxScaleConstraint>();
            scaleHandler.ScaleMinimum = minScale;
            scaleHandler.ScaleMaximum = maxScale;
            boundsControl.RegisterTransformScaleHandler(scaleHandler);

            Vector3 initialScale = boundsControl.transform.localScale;

            const int numHandSteps = 1;

            Vector3 initialHandPosition = new Vector3(0, 0, 0.5f);
            var frontRightCornerPos = boundsControl.gameObject.transform.Find("rigRoot/corner_3").position; // front right corner is corner 3
            TestHand hand = new TestHand(Handedness.Right);

            // Hands grab object at initial position
            yield return hand.Show(initialHandPosition);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.OpenSteadyGrabPoint);
            yield return hand.MoveTo(frontRightCornerPos, numHandSteps);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Pinch);

            // No change to scale yet
            Assert.AreEqual(initialScale, boundsControl.transform.localScale);

            // Move hands beyond max scale limit
            yield return hand.MoveTo(new Vector3(scaleHandler.ScaleMaximum * 2, scaleHandler.ScaleMaximum * 2, 0) + frontRightCornerPos, numHandSteps);

            // Assert scale at max
            Assert.AreEqual(Vector3.one * scaleHandler.ScaleMaximum, boundsControl.transform.localScale);

            // Move hands beyond min scale limit
            yield return hand.MoveTo(new Vector3(-scaleHandler.ScaleMinimum * 2, -scaleHandler.ScaleMinimum * 2, 0) + frontRightCornerPos, numHandSteps);

            // Assert scale at min
            Assert.AreEqual(Vector3.one * scaleHandler.ScaleMinimum, boundsControl.transform.localScale);

            GameObject.Destroy(boundsControl.gameObject);
            // Wait for a frame to give Unity a change to actually destroy the object
            yield return null;

        }

        /// <summary>
        /// Uses far interaction (HoloLens 1 style) to scale the bounds control
        /// </summary>
        [UnityTest]
        public IEnumerator ScaleViaHoloLens1Interaction()
        {
            BoundsControl boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);
            BoxCollider boxCollider = boundsControl.GetComponent<BoxCollider>();
            PlayModeTestUtilities.PushHandSimulationProfile();
            PlayModeTestUtilities.SetHandSimulationMode(HandSimulationMode.Gestures);

            CameraCache.Main.transform.LookAt(boundsControl.gameObject.transform.Find("rigRoot/corner_3").transform);

            var startHandPos = CameraCache.Main.transform.TransformPoint(new Vector3(0.1f, 0f, 1.5f));
            TestHand rightHand = new TestHand(Handedness.Right);
            yield return rightHand.Show(startHandPos);
            yield return rightHand.SetGesture(ArticulatedHandPose.GestureId.Pinch);
            // After pinching, center should remain the same
            var afterPinchbounds = boxCollider.bounds;
            TestUtilities.AssertAboutEqual(afterPinchbounds.center, boundsControlStartCenter, "boundsControl incorrect center after pinch");
            TestUtilities.AssertAboutEqual(afterPinchbounds.size, boundsControlStartScale, "boundsControl incorrect size after pinch");

            var delta = new Vector3(0.1f, 0.1f, 0f);
            yield return rightHand.Move(delta);

            var endBounds = boxCollider.bounds;
            Vector3 expectedCenter = new Vector3(0.028f, 0.028f, 1.47f);
            Vector3 expectedSize = Vector3.one * .555f;
            TestUtilities.AssertAboutEqual(endBounds.center, expectedCenter, "endBounds incorrect center");
            TestUtilities.AssertAboutEqual(endBounds.size, expectedSize, "endBounds incorrect size", 0.02f);

            GameObject.Destroy(boundsControl.gameObject);
            // Wait for a frame to give Unity a change to actually destroy the object
            yield return null;

            // Restore the input simulation profile
            PlayModeTestUtilities.PopHandSimulationProfile();

            yield return null;
        }

        /// <summary>
        /// Test that changing the transform of the bounds control target (rotation, scale, translation)
        /// updates the rig bounds
        /// </summary>
        [UnityTest]
        public IEnumerator UpdateTransformUpdatesBounds()
        {
            BoundsControl boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);
            boundsControl.HideElementsInInspector = false;
            yield return null;

            var startBounds = GetBoundsControlRigBounds(boundsControl);
            TestUtilities.AssertAboutEqual(startBounds.center, boundsControlStartCenter, "boundsControl incorrect center at start");
            TestUtilities.AssertAboutEqual(startBounds.size, boundsControlStartScale, "boundsControl incorrect size at start");

            boundsControl.gameObject.transform.localScale *= 2;
            yield return null;

            var afterScaleBounds = GetBoundsControlRigBounds(boundsControl);
            var scaledSize = boundsControlStartScale * 2;
            TestUtilities.AssertAboutEqual(afterScaleBounds.center, boundsControlStartCenter, "boundsControl incorrect center after scale");
            TestUtilities.AssertAboutEqual(afterScaleBounds.size, scaledSize, "boundsControl incorrect size after scale");

            boundsControl.gameObject.transform.position += Vector3.one;
            yield return null;
            var afterTranslateBounds = GetBoundsControlRigBounds(boundsControl);
            var afterTranslateCenter = Vector3.one + boundsControlStartCenter;

            TestUtilities.AssertAboutEqual(afterTranslateBounds.center, afterTranslateCenter, "boundsControl incorrect center after translate");
            TestUtilities.AssertAboutEqual(afterTranslateBounds.size, scaledSize, "boundsControl incorrect size after translate");

            var c0 = boundsControl.gameObject.transform.Find("rigRoot/corner_0");
            var boundsControlBottomCenter = afterTranslateBounds.center - Vector3.up * afterTranslateBounds.extents.y;
            Vector3 cc0 = c0.position - boundsControlBottomCenter;
            float rotateAmount = 30;
            boundsControl.gameObject.transform.Rotate(new Vector3(0, rotateAmount, 0));
            yield return null;
            Vector3 cc0_rotated = c0.position - boundsControlBottomCenter;
            Assert.AreApproximatelyEqual(Vector3.Angle(cc0, cc0_rotated), 30, $"rotated angle is not correct. expected {rotateAmount} but got {Vector3.Angle(cc0, cc0_rotated)}");

            GameObject.Destroy(boundsControl.gameObject);
        }

        /// <summary>
        /// Ensure that while using BoundingBox, if that object gets
        /// deactivated, that BoundingBox no longer transforms that object.
        /// </summary>
        [UnityTest]
        public IEnumerator DisableObject()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);

            Vector3 initialScale = boundsControl.transform.localScale;

            const int numHandSteps = 1;

            Vector3 initialHandPosition = new Vector3(0, 0, 0.5f);
            var frontRightCornerPos = boundsControl.gameObject.transform.Find("rigRoot/corner_3").position; // front right corner is corner 3
            TestHand hand = new TestHand(Handedness.Right);

            // Hands grab object at initial position
            yield return hand.Show(initialHandPosition);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.OpenSteadyGrabPoint);
            yield return hand.MoveTo(frontRightCornerPos, numHandSteps);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Pinch);

            // Verify that scale works before deactivating
            yield return hand.Move(Vector3.right * 0.2f, numHandSteps);
            Vector3 afterTransformScale = boundsControl.transform.localScale;
            Assert.AreNotEqual(initialScale, afterTransformScale);

            // Deactivate object and ensure that we don't scale
            boundsControl.gameObject.SetActive(false);
            yield return null;
            boundsControl.gameObject.SetActive(true);
            yield return hand.Move(Vector3.right * 0.2f, numHandSteps);
            Assert.AreEqual(afterTransformScale, boundsControl.transform.localScale);
        }

        /// <summary>
        /// Tests proximity scaling on scale handles of bounds control
        /// Verifies default behavior of handles with effect enabled / disabled as well as custom runtime configured scaling / distance values
        /// </summary>
        [UnityTest]
        public IEnumerator ProximityOnScaleHandles()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);

            // 1. test no proximity scaling active per default
            ScaleHandlesConfiguration scaleHandleConfig = boundsControl.ScaleHandlesConfig;
            Vector3 defaultHandleSize = Vector3.one * scaleHandleConfig.HandleSize;

            Vector3 initialHandPosition = new Vector3(0, 0, 0f);
            // this is specific to scale handles
            Transform scaleHandle = boundsControl.gameObject.transform.Find("rigRoot/corner_3");
            Transform proximityScaledVisual = scaleHandle.GetChild(0)?.GetChild(0);
            var frontRightCornerPos = scaleHandle.position; // front right corner is corner 
            Assert.IsNotNull(proximityScaledVisual, "Couldn't get visual gameobject for scale handle");
            Assert.IsTrue(proximityScaledVisual.name == "visuals", "scale visual has unexpected name");

            yield return null;
            // verify no proximity scaling applied per default
            Assert.AreEqual(proximityScaledVisual.localScale, defaultHandleSize, "Handle was scaled even though proximity effect wasn't active");
            TestHand hand = new TestHand(Handedness.Left);
            Vector3 initialScale = boundsControl.transform.localScale;

            // Hands grab object at initial position
            yield return hand.Show(initialHandPosition);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.OpenSteadyGrabPoint);
            yield return hand.MoveTo(frontRightCornerPos);
            yield return null;

            // we're in proximity scaling range - check if proximity scaling wasn't applied
            Assert.AreEqual(proximityScaledVisual.localScale, defaultHandleSize, "Handle was scaled even though proximity effect wasn't active");

            //// reset hand
            yield return hand.MoveTo(initialHandPosition);

            // 2. enable proximity scaling and test defaults
            ProximityEffectConfiguration proximityConfig = boundsControl.HandleProximityEffectConfig;
            proximityConfig.ProximityEffectActive = true;
            proximityConfig.CloseGrowRate = 1.0f;
            proximityConfig.MediumGrowRate = 1.0f;
            proximityConfig.FarGrowRate = 1.0f;
            yield return null; // wait so rig gameobjects get recreated
            yield return TestCurrentProximityConfiguration(boundsControl, hand, "Defaults");

            // reset hand
            yield return hand.MoveTo(initialHandPosition);

            // 3. now test custom configuration is applied during runtime
            proximityConfig.CloseScale = 4.0f;
            proximityConfig.MediumScale = 3.0f;
            proximityConfig.FarScale = 2.0f;

            proximityConfig.ObjectMediumProximity = 0.2f;
            proximityConfig.ObjectCloseProximity = 0.1f;

            yield return null; // wait so rig gameobjects get recreated
            yield return TestCurrentProximityConfiguration(boundsControl, hand, "Custom runtime config max");
        }

        /// <summary>
        /// This tests far, medium and close proximity scaling on scale handles by moving the test hand in the corresponding distance ranges
        /// </summary>
        /// <param name="boundsControl">Bounds Control to test on</param>
        /// <param name="hand">Test hand to use for testing proximity to handle</param>
        private IEnumerator TestCurrentProximityConfiguration(BoundsControl boundsControl, TestHand hand, string testDescription)
        {
            // get config and scaling handle
            ScaleHandlesConfiguration scaleHandleConfig = boundsControl.ScaleHandlesConfig;
            Vector3 defaultHandleSize = Vector3.one * scaleHandleConfig.HandleSize;
            Transform scaleHandle = boundsControl.gameObject.transform.Find("rigRoot/corner_3");
            Transform proximityScaledVisual = scaleHandle.GetChild(0)?.GetChild(0);
            var frontRightCornerPos = scaleHandle.position;
            // check far scale applied
            ProximityEffectConfiguration proximityConfig = boundsControl.HandleProximityEffectConfig;
            Vector3 expectedFarScale = defaultHandleSize * proximityConfig.FarScale;
            Assert.AreEqual(proximityScaledVisual.localScale, expectedFarScale, testDescription + " - Proximity far scale wasn't applied to handle");

            // move into medium range and check if scale was applied
            Vector3 mediumProximityTestDist = frontRightCornerPos;
            mediumProximityTestDist.x += proximityConfig.ObjectMediumProximity;
            yield return hand.MoveTo(mediumProximityTestDist);
            Vector3 expectedMediumScale = defaultHandleSize * proximityConfig.MediumScale;
            Assert.AreEqual(proximityScaledVisual.localScale, expectedMediumScale, testDescription + " - Proximity medium scale wasn't applied to handle");

            // move into close scale range and check if scale was applied
            Vector3 closeProximityTestDir = frontRightCornerPos;
            closeProximityTestDir.x += proximityConfig.ObjectCloseProximity;
            yield return hand.MoveTo(closeProximityTestDir);
            Vector3 expectedCloseScale = defaultHandleSize * proximityConfig.CloseScale;
            Assert.AreEqual(proximityScaledVisual.localScale, expectedCloseScale, testDescription + " - Proximity close scale wasn't applied to handle");

            // move out of close scale again - should fall back to medium proximity
            closeProximityTestDir = mediumProximityTestDist;
            yield return hand.MoveTo(closeProximityTestDir);
            Assert.AreEqual(proximityScaledVisual.localScale, expectedMediumScale, testDescription + " - Proximity medium scale wasn't applied to handle");

            // move out of medium proximity and check if far scaling is applied
            mediumProximityTestDist = Vector3.zero;
            yield return hand.MoveTo(mediumProximityTestDist);
            Assert.AreEqual(proximityScaledVisual.localScale, expectedFarScale, testDescription + " - Proximity far scale wasn't applied to handle");

            yield return null;
        }

        /// <summary>
        /// Tests setting a target in code that is a different gameobject than the gameobject the bounds control component is attached to
        /// </summary>
        [UnityTest]
        public IEnumerator SetTarget()
        {
            // create cube without control
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = boundsControlStartCenter;

            MixedRealityPlayspace.PerformTransformation(
            p =>
            {
                p.position = Vector3.zero;
                p.LookAt(cube.transform.position);
            });

            cube.transform.localScale = boundsControlStartScale;

            // create another gameobject with boundscontrol attached 
            var emptyGameObject = new GameObject("empty");
            BoundsControl boundsControl = emptyGameObject.AddComponent<BoundsControl>();
            yield return new WaitForFixedUpdate();

            // fetch root and scale handle
            GameObject rigRoot = boundsControl.transform.Find("rigRoot").gameObject;
            Assert.IsNotNull(rigRoot, "rigRoot couldn't be found");
            var scaleHandle = boundsControl.transform.Find("rigRoot/corner_3");
            Assert.IsNotNull(scaleHandle, "scale handle couldn't be found");

            // verify root is parented to bounds control gameobject
            Assert.AreEqual(boundsControl.gameObject, rigRoot.transform.parent.gameObject);

            // set target to cube
            boundsControl.Target = cube;
            Assert.IsNotNull(rigRoot, "rigRoot was destroyed on setting a new target");
            Assert.IsNotNull(scaleHandle, "scale handle was destroyed on setting a new target");

            // verify root is parented to target gameobject
            Assert.AreEqual(cube, rigRoot.transform.parent.gameObject);

            // grab corner and scale object
            Vector3 initialHandPosition = new Vector3(0, 0, 0.5f);
            int numSteps = 30;
            var delta = new Vector3(0.1f, 0.1f, 0f);
            TestHand hand = new TestHand(Handedness.Right);
            yield return hand.Show(initialHandPosition);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.OpenSteadyGrabPoint);
            var scaleHandlePos = scaleHandle.position;
            yield return hand.MoveTo(scaleHandlePos, numSteps);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Pinch);
            yield return hand.MoveTo(scaleHandlePos + delta, numSteps);

            var endBounds = cube.GetComponent<BoxCollider>().bounds;
            Vector3 expectedCenter = new Vector3(0.033f, 0.033f, 1.467f);
            Vector3 expectedSize = Vector3.one * .567f;
            TestUtilities.AssertAboutEqual(endBounds.center, expectedCenter, "endBounds incorrect center");
            TestUtilities.AssertAboutEqual(endBounds.size, expectedSize, "endBounds incorrect size");

            Object.Destroy(emptyGameObject);
            Object.Destroy(cube);
            // Wait for a frame to give Unity a change to actually destroy the object
            yield return null;
        }

        /// <summary>
        /// Tests the different activation flags bounding box handles can be activated with
        /// </summary>
        [UnityTest]
        public IEnumerator ActivationTypeTest()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);
            boundsControl.gameObject.AddComponent<NearInteractionGrabbable>();

            // cache rig root for verifying that we're not recreating the rig on config changes
            GameObject rigRoot = boundsControl.transform.Find("rigRoot").gameObject;
            Assert.IsNotNull(rigRoot, "rigRoot couldn't be found");

            // default case is activation on start
            Assert.IsTrue(boundsControl.Active, "default behavior should be bounds control activation on start");
            Assert.IsFalse(boundsControl.WireframeOnly, "default behavior should be not wireframe only");

            boundsControl.BoundsControlActivation = BoundsControlActivationType.ActivateByProximity;
            // make sure rigroot is still alive
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            // handles should be disabled now 
            Assert.IsTrue(boundsControl.Active, "control should be active");
            Assert.IsTrue(boundsControl.WireframeOnly, "wireframeonly should be enabled");

            // move to bounds control with hand and check if it activates on proximity
            Transform cornerVisual = rigRoot.transform.Find("corner_1/visualsScale/visuals");
            Assert.IsNotNull(cornerVisual, "couldn't find scale handle visual");
            TestHand hand = new TestHand(Handedness.Right);
            Vector3 pointOnCube = new Vector3(-0.033f, -0.129f, 0.499f); // position where hand ray points on center of the test cube
            yield return hand.Show(pointOnCube);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Open);
            Vector3 pointOnCubeNear = boundsControl.transform.position;
            pointOnCubeNear.z = cornerVisual.position.z;
            yield return hand.MoveTo(pointOnCubeNear);
            yield return PlayModeTestUtilities.WaitForInputSystemUpdate();
            Assert.IsTrue(boundsControl.Active, "control should be active");
            Assert.IsFalse(boundsControl.WireframeOnly, "wireframeonly should be disabled");

            yield return hand.MoveTo(pointOnCube);
            Assert.IsTrue(boundsControl.Active, "control should be active");
            Assert.IsTrue(boundsControl.WireframeOnly, "wireframeonly should be enabled");
            yield return hand.Hide();

            // check far pointer activation
            boundsControl.BoundsControlActivation = BoundsControlActivationType.ActivateByPointer;
            yield return hand.Show(cornerVisual.position);
            yield return PlayModeTestUtilities.WaitForInputSystemUpdate();
            // shouldn't be enabled on proximity of near pointer
            Assert.IsTrue(boundsControl.Active, "control should be active");
            Assert.IsTrue(boundsControl.WireframeOnly, "wireframeonly should be enabled");
            // enable on far pointer
            yield return hand.MoveTo(pointOnCube);

            yield return PlayModeTestUtilities.WaitForInputSystemUpdate();
            Assert.IsTrue(boundsControl.Active, "control should be active");
            Assert.IsFalse(boundsControl.WireframeOnly, "wireframeonly should be disabled");
            yield return hand.Hide();
            Assert.IsTrue(boundsControl.WireframeOnly, "wireframeonly should be enabled");

            boundsControl.BoundsControlActivation = BoundsControlActivationType.ActivateByProximityAndPointer;
            yield return hand.Show(cornerVisual.position);
            yield return PlayModeTestUtilities.WaitForInputSystemUpdate();
            // should be enabled on proximity of near pointer
            Assert.IsTrue(boundsControl.Active, "control should be active");
            Assert.IsFalse(boundsControl.WireframeOnly, "wireframeonly should be disabled");
            // enable on far pointer
            yield return hand.MoveTo(pointOnCube);
            yield return PlayModeTestUtilities.WaitForInputSystemUpdate();
            Assert.IsTrue(boundsControl.Active, "control should be active");
            Assert.IsFalse(boundsControl.WireframeOnly, "wireframeonly should be disabled");
            yield return hand.Hide();

            // check manual activation
            boundsControl.BoundsControlActivation = BoundsControlActivationType.ActivateManually;
            Assert.IsFalse(boundsControl.Active, "control shouldn't be active");

            boundsControl.Active = true;
            Assert.IsTrue(boundsControl.Active, "control should be active");
            Assert.IsFalse(boundsControl.WireframeOnly, "wireframeonly should be disabled");
            yield return null;
        }

        /// <summary>
        /// Tests visibility changes of different handle types: scale, rotateX, rotateY, rotateZ.
        /// Makes sure rig isn't recreated and visibility restores as expected when disabling the entire control.
        /// </summary>
        [UnityTest]
        public IEnumerator HandleVisibilityTest()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);

            // cache rig root for verifying that we're not recreating the rig on config changes
            GameObject rigRoot = boundsControl.transform.Find("rigRoot").gameObject;
            Assert.IsNotNull(rigRoot, "rigRoot couldn't be found");

            // cache rig root for verifying that we're not recreating the rig on config changes
            Transform scaleHandle = rigRoot.transform.Find("corner_3");
            Assert.IsNotNull(scaleHandle, "couldn't find handle");

            // test scale handle behavior
            Assert.IsTrue(scaleHandle.gameObject.activeSelf, "scale handle not active by default");
            ScaleHandlesConfiguration scaleHandleConfig = boundsControl.ScaleHandlesConfig;
            scaleHandleConfig.ShowScaleHandles = false;
            Assert.IsNotNull(rigRoot, "rigRoot was destroyed on hiding handles");
            Assert.IsNotNull(scaleHandle, "handle was destroyed on hide");
            Assert.IsFalse(scaleHandle.gameObject.activeSelf, "handle wasn't disabled on hide");

            scaleHandleConfig.ShowScaleHandles = true;
            Assert.IsTrue(scaleHandle.gameObject.activeSelf, "handle wasn't enabled on show");

            // test rotation handle behavior
            Transform rotationHandleAxisX = rigRoot.transform.Find("midpoint_0");
            Transform rotationHandleAxisY = rigRoot.transform.Find("midpoint_1");
            Transform rotationHandleAxisZ = rigRoot.transform.Find("midpoint_8");
            Assert.IsTrue(rotationHandleAxisX.gameObject.activeSelf, "rotation handle x not active by default");
            Assert.IsTrue(rotationHandleAxisY.gameObject.activeSelf, "rotation handle y not active by default");
            Assert.IsTrue(rotationHandleAxisZ.gameObject.activeSelf, "rotation handle z not active by default");
            RotationHandlesConfiguration rotationHandlesConfig = boundsControl.RotationHandlesConfig;

            // disable visibility for each component
            rotationHandlesConfig.ShowRotationHandleForX = false;
            Assert.IsNotNull(rigRoot, "rigRoot was destroyed on hiding handles");
            Assert.IsNotNull(rotationHandleAxisX, "handle was destroyed on hide");
            Assert.IsFalse(rotationHandleAxisX.gameObject.activeSelf, "rotation handle x not hidden");
            Assert.IsTrue(rotationHandleAxisY.gameObject.activeSelf, "rotation handle y not active");
            Assert.IsTrue(rotationHandleAxisZ.gameObject.activeSelf, "rotation handle z not active");

            rotationHandlesConfig.ShowRotationHandleForY = false;
            Assert.IsFalse(rotationHandleAxisX.gameObject.activeSelf, "rotation handle x not hidden");
            Assert.IsFalse(rotationHandleAxisY.gameObject.activeSelf, "rotation handle y not hidden");
            Assert.IsTrue(rotationHandleAxisZ.gameObject.activeSelf, "rotation handle z not active");

            rotationHandlesConfig.ShowRotationHandleForX = true;
            rotationHandlesConfig.ShowRotationHandleForY = true;
            rotationHandlesConfig.ShowRotationHandleForZ = false;
            Assert.IsTrue(rotationHandleAxisX.gameObject.activeSelf, "rotation handle x not active");
            Assert.IsTrue(rotationHandleAxisY.gameObject.activeSelf, "rotation handle y not active");
            Assert.IsFalse(rotationHandleAxisZ.gameObject.activeSelf, "rotation handle z not hidden");

            // make sure handles are disabled and enabled when bounds control is deactivated / activated
            boundsControl.Active = false;
            Assert.IsNotNull(rigRoot, "rigRoot was destroyed on disabling bounds control");
            Assert.IsFalse(scaleHandle.gameObject.activeSelf, "scale handle not disabled");
            Assert.IsFalse(rotationHandleAxisX.gameObject.activeSelf, "rotation handle x not hidden");
            Assert.IsFalse(rotationHandleAxisY.gameObject.activeSelf, "rotation handle y not hidden");
            Assert.IsFalse(rotationHandleAxisZ.gameObject.activeSelf, "rotation handle z not hidden");

            // set active again and make sure internal states have been restored
            boundsControl.Active = true;
            Assert.IsNotNull(rigRoot, "rigRoot was destroyed on enabling bounds control");
            Assert.IsTrue(scaleHandle.gameObject.activeSelf, "scale handle not enabled");
            Assert.IsTrue(rotationHandleAxisX.gameObject.activeSelf, "rotation handle x not active");
            Assert.IsTrue(rotationHandleAxisY.gameObject.activeSelf, "rotation handle y not active");
            Assert.IsFalse(rotationHandleAxisZ.gameObject.activeSelf, "rotation handle z not hidden");

            // enable z axis again and verify
            rotationHandlesConfig.ShowRotationHandleForZ = true;
            Assert.IsTrue(rotationHandleAxisX.gameObject.activeSelf, "rotation handle x not active");
            Assert.IsTrue(rotationHandleAxisY.gameObject.activeSelf, "rotation handle y not active");
            Assert.IsTrue(rotationHandleAxisZ.gameObject.activeSelf, "rotation handle z not active");

            yield return null;
        }

        /// <summary>
        /// Tests that draw tether flag gets propagated to NearInteractionGrabbable on configuration changes.
        /// Makes sure that rig / visuals aren't recreated.
        /// </summary>
        [UnityTest]
        public IEnumerator ManipulationTetherTest()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);

            // cache rig root for verifying that we're not recreating the rig on config changes
            GameObject rigRoot = boundsControl.transform.Find("rigRoot").gameObject;
            Assert.IsNotNull(rigRoot, "rigRoot couldn't be found");

            // test default and runtime changing draw tether flag of both handle types
            yield return TestDrawManipulationTetherFlag(boundsControl.ScaleHandlesConfig, rigRoot, "corner_3");
            yield return TestDrawManipulationTetherFlag(boundsControl.RotationHandlesConfig, rigRoot, "midpoint_2");
            yield return null;
        }

        private IEnumerator TestDrawManipulationTetherFlag(HandlesBaseConfiguration config, GameObject rigRoot, string handleName)
        {
            Assert.IsTrue(config.DrawTetherWhenManipulating, "tether drawing should be on as default");

            // cache rig root for verifying that we're not recreating the rig on config changes
            Transform handle = rigRoot.transform.Find(handleName);
            Assert.IsNotNull(handle, "couldn't find handle");
            var grabbable = handle.gameObject.GetComponent<NearInteractionGrabbable>();
            Assert.AreEqual(config.DrawTetherWhenManipulating, grabbable.ShowTetherWhenManipulating, "draw tether wasn't propagated to handle NearInteractionGrabbable component");

            config.DrawTetherWhenManipulating = false;
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNotNull(handle, "handle was destroyed when changing tether visibility");
            Assert.IsFalse(grabbable.ShowTetherWhenManipulating, "show tether wasn't applied to NearInteractionGrabbable of handle");

            yield return null;
        }

        /// <summary>
        /// Tests adding padding to the bounds of a bounds control and verifies if handles have moved accordingly.
        /// Also verifies that visuals didn't get recreated during padding value changes.
        /// </summary>
        [UnityTest]
        public IEnumerator BoundsControlPaddingTest()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);

            // fetch rigroot
            GameObject rigRoot = boundsControl.transform.Find("rigRoot").gameObject;
            Assert.IsNotNull(rigRoot, "rigRoot couldn't be found");
            Transform cornerVisual = rigRoot.transform.Find("corner_3/visualsScale/visuals");
            Assert.IsNotNull(cornerVisual, "couldn't find corner visual");
            var cornerVisualPosition = cornerVisual.position;
            var defaultPadding = boundsControl.BoxPadding;
            var targetBoundsOriginal = boundsControl.TargetBounds; // this has the default padding already applied
            var targetBoundsSize = targetBoundsOriginal.size;
            Vector3 targetBoundsScaleInv = new Vector3(1.0f / targetBoundsOriginal.transform.lossyScale.x, 1.0f / targetBoundsOriginal.transform.lossyScale.y, 1.0f / targetBoundsOriginal.transform.lossyScale.z);

            // set padding
            boundsControl.BoxPadding = Vector3.one * 0.5f;
            var scaledPaddingDelta = Vector3.Scale(boundsControl.BoxPadding - defaultPadding, targetBoundsScaleInv);
            yield return PlayModeTestUtilities.WaitForInputSystemUpdate();

            // check rig or handle isn't recreated
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNotNull(cornerVisual, "handle visual was recreated on changing padding");

            // check padding is applied to bounds 
            var newBoundsSize = boundsControl.TargetBounds.size;
            Assert.AreEqual(newBoundsSize, targetBoundsSize + scaledPaddingDelta, "padding wasn't applied to target bounds");

            // check padding is applied to handle position - corners should have moved half the padding distance 
            var cornerPosDiff = cornerVisualPosition - cornerVisual.position;
            var paddingHalf = boundsControl.BoxPadding * 0.5f;
            Assert.AreEqual(cornerPosDiff.sqrMagnitude, paddingHalf.sqrMagnitude, "corner visual didn't move on applying padding to control");

            yield return null;
        }

        /// <summary>
        /// Tests toggling link visibility and verifying visuals are not recreated.
        /// </summary>
        [UnityTest]
        public IEnumerator LinksVisibilityTest()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);

            // fetch rigroot, corner visual and rotation handle config
            GameObject rigRoot = boundsControl.transform.Find("rigRoot").gameObject;
            Assert.IsNotNull(rigRoot, "rigRoot couldn't be found");

            Transform linkVisual = rigRoot.transform.Find("link_0");
            Assert.IsNotNull(linkVisual, "link visual couldn't be found");
            Assert.IsTrue(linkVisual.gameObject.activeSelf, "links not visible by default");
            yield return new WaitForFixedUpdate();

            // disable wireframe and make sure we're not recreating anything
            LinksConfiguration linkConfiguration = boundsControl.LinksConfig;
            linkConfiguration.ShowWireFrame = false;
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNotNull(linkVisual, "link visual was recreated on changing visibility");
            Assert.IsFalse(linkVisual.gameObject.activeSelf, "links did not get deactivated on hide");
            yield return new WaitForFixedUpdate();

            // enable links again
            linkConfiguration.ShowWireFrame = true;
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNotNull(linkVisual, "link visual was recreated on changing visibility");
            Assert.IsTrue(linkVisual.gameObject.activeSelf, "links did not get activated on show");
            yield return new WaitForFixedUpdate();

            yield return null;
        }

        /// <summary>
        /// Verifies links are getting disabled on flattening the bounds control without recreating any visuals
        /// </summary>
        [UnityTest]
        public IEnumerator LinksFlattenTest()
        {
            // test flatten and unflatten for links
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);

            // fetch rigroot, and one of the link visuals
            GameObject rigRoot = boundsControl.transform.Find("rigRoot").gameObject;
            Assert.IsNotNull(rigRoot, "rigRoot couldn't be found");

            Transform linkVisual = rigRoot.transform.Find("link_0");
            Assert.IsNotNull(linkVisual, "link visual couldn't be found");

            Assert.IsTrue(linkVisual.gameObject.activeSelf, "link with index 0 wasn't enabled by default");

            // flatten x axis and make sure link gets deactivated
            boundsControl.FlattenAxis = FlattenModeType.FlattenX;
            Assert.IsFalse(linkVisual.gameObject.activeSelf, "link with index 0 wasn't disabled when control was flattened in X axis");
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");

            // unflatten the control again and make sure link gets activated accordingly
            boundsControl.FlattenAxis = FlattenModeType.DoNotFlatten;
            Assert.IsTrue(linkVisual.gameObject.activeSelf, "link with index 0 wasn't enabled on unflatten");
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            yield return null;
        }

        /// <summary>
        /// Tests link radius config changes are applied to the link visual without recreating them.
        /// </summary>
        [UnityTest]
        public IEnumerator LinksRadiusTest()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);

            // fetch rigroot, and one of the link visuals
            GameObject rigRoot = boundsControl.transform.Find("rigRoot").gameObject;
            Assert.IsNotNull(rigRoot, "rigRoot couldn't be found");

            Transform linkVisual = rigRoot.transform.Find("link_0");
            Assert.IsNotNull(linkVisual, "link visual couldn't be found");

            LinksConfiguration linkConfiguration = boundsControl.LinksConfig;
            // verify default radius
            Assert.AreEqual(linkVisual.localScale.x, linkConfiguration.WireframeEdgeRadius, "Wireframe default edge radius wasn't applied to link local scale");
            // change radius
            linkConfiguration.WireframeEdgeRadius = 0.5f;
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNotNull(linkVisual, "link visual shouldn't be destroyed when changing edge radius");

            // check if radius was applied
            Assert.AreEqual(linkVisual.localScale.x, linkConfiguration.WireframeEdgeRadius, "Wireframe edge radius wasn't applied to link local scale");

            yield return null;
        }


        /// <summary>
        /// Verifies link shapes get applied to link visuals once they are changed in the configuration.
        /// Makes sure links are not destroyed but only mesh filter gets replaced.
        /// </summary>
        [UnityTest]
        public IEnumerator LinksShapeTest()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);

            // fetch rigroot, and one of the link visuals
            GameObject rigRoot = boundsControl.transform.Find("rigRoot").gameObject;
            Assert.IsNotNull(rigRoot, "rigRoot couldn't be found");

            Transform linkVisual = rigRoot.transform.Find("link_0");
            Assert.IsNotNull(linkVisual, "link visual couldn't be found");

            LinksConfiguration linkConfiguration = boundsControl.LinksConfig;
            // verify default shape
            Assert.AreEqual(linkConfiguration.WireframeShape, WireframeType.Cubic);
            var linkMeshFilter = linkVisual.GetComponent<MeshFilter>();

            Assert.IsTrue(linkMeshFilter.mesh.name == "Cube Instance", "Links weren't created with default cube");

            // change shape - this should only affect the sharedmesh property of the mesh filter
            linkConfiguration.WireframeShape = WireframeType.Cylindrical;
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNotNull(linkVisual, "link visual shouldn't be destroyed when switching mesh");

            // check if shape was applied
            Assert.IsTrue(linkMeshFilter.mesh.name == "Cylinder Instance", "Link shape wasn't switched to cylinder");

            yield return null;
        }

        /// <summary>
        /// Tests changing the links material during runtime and making sure links and rig are not recreated.
        /// </summary>
        [UnityTest]
        public IEnumerator LinksMaterialTest()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);

            // fetch rigroot and one of the link visuals
            GameObject rigRoot = boundsControl.transform.Find("rigRoot").gameObject;
            Assert.IsNotNull(rigRoot, "rigRoot couldn't be found");

            Transform linkVisual = rigRoot.transform.Find("link_0");
            Assert.IsNotNull(linkVisual, "link visual couldn't be found");
            LinksConfiguration linkConfiguration = boundsControl.LinksConfig;
            // set material and make sure rig root and link isn't destroyed while doing so
            linkConfiguration.WireframeMaterial = testMaterial;
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNotNull(linkVisual, "link visual was recreated on setting material");
            // make sure color changed on visual
            Assert.AreEqual(linkVisual.GetComponent<Renderer>().material.color, testMaterial.color, "wireframe material wasn't applied to visual");

            yield return null;
        }

        /// <summary>
        /// Tests changing the box display default and grabbed material during runtime,
        /// making sure neither box display nor rig get recreated.
        /// </summary>
        [UnityTest]
        public IEnumerator BoxDisplayMaterialTest()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);

            // fetch rigroot, corner visual and rotation handle config
            GameObject rigRoot = boundsControl.transform.Find("rigRoot").gameObject;
            Assert.IsNotNull(rigRoot, "rigRoot couldn't be found");
            // box visual should be disabled per default
            Transform boxVisual = rigRoot.transform.Find("box display");
            Assert.IsNotNull(boxVisual, "box visual couldn't be found");
            Assert.IsFalse(boxVisual.gameObject.activeSelf, "box was active as default - correct behavior is box display being disabled as a default");

            BoxDisplayConfiguration boxConfig = boundsControl.BoxDisplayConfig;
            // set materials and make 1. rig root hasn't been destroyed 2. box hasn't been destroyed 3. box has been activated
            boxConfig.BoxMaterial = testMaterial;
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNotNull(boxVisual, "box visual was recreated on setting material");
            Assert.IsTrue(boxVisual.gameObject.activeSelf, "box wasn't set active when setting the material");
            // now set grabbed material and make sure we neither destroy rig root nor the box display
            boxConfig.BoxGrabbedMaterial = testMaterialGrabbed;
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNotNull(boxVisual, "box visual got destroyed when setting grabbed material");
            // make sure color changed on visual
            Assert.AreEqual(boxVisual.GetComponent<Renderer>().material.color, testMaterial.color, "box material wasn't applied to visual");

            // grab one of the scale handles and make sure grabbed material is applied to box
            Transform cornerVisual = rigRoot.transform.Find("corner_3/visualsScale/visuals");
            Assert.IsNotNull(cornerVisual, "couldn't find scale handle visual");
            TestHand hand = new TestHand(Handedness.Right);
            yield return hand.Show(Vector3.zero);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.OpenSteadyGrabPoint);
            yield return hand.MoveTo(cornerVisual.position);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Pinch);
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(boxVisual.GetComponent<Renderer>().material.color, testMaterialGrabbed.color, "box grabbed material wasn't applied to visual");
            // release handle
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.OpenSteadyGrabPoint);

            yield return null;
        }

        /// <summary>
        /// Tests scaling of box display after flattening bounds control during runtime
        /// and making sure neither box display nor rig get recreated.
        /// </summary>
        [UnityTest]
        public IEnumerator BoxDisplayFlattenAxisScaleTest()
        {
            // test flatten mode of rotation handle
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);

            // cache rig root for verifying that we're not recreating the rig on config changes
            GameObject rigRoot = boundsControl.transform.Find("rigRoot").gameObject;
            Assert.IsNotNull(rigRoot, "rigRoot couldn't be found");

            // get box display and activate by setting material
            BoxDisplayConfiguration boxDisplayConfig = boundsControl.BoxDisplayConfig;
            boxDisplayConfig.BoxMaterial = testMaterial;
            boundsControl.FlattenAxis = FlattenModeType.DoNotFlatten;

            Transform boxDisplay = rigRoot.transform.Find("box display");
            Assert.IsNotNull(boxDisplay, "couldn't find box display");
            Assert.IsTrue(boxDisplay.gameObject.activeSelf, "box should be active when material is set");
            Vector3 originalScale = boxDisplay.localScale;

            // flatten x axis and make sure box gets flattened
            boundsControl.FlattenAxis = FlattenModeType.FlattenX;
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNotNull(boxDisplay, "box display got destroyed while flattening axis");
            Assert.AreEqual(boxDisplay.localScale.x, boxDisplayConfig.FlattenAxisDisplayScale, "Flatten axis scale wasn't applied properly to box display");

            // modify flatten scale
            boxDisplayConfig.FlattenAxisDisplayScale = 5.0f;
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNotNull(boxDisplay, "box display got destroyed while changing flatten scale");
            Assert.AreEqual(boxDisplay.localScale.x * boundsControl.transform.localScale.x, boxDisplayConfig.FlattenAxisDisplayScale, "Flatten axis scale wasn't applied properly to box display");

            // unflatten the control again and make sure handle gets activated accordingly
            boundsControl.FlattenAxis = FlattenModeType.DoNotFlatten;
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNotNull(boxDisplay, "box display got destroyed while unflattening control");
            Assert.AreEqual(originalScale, boxDisplay.localScale, "Unflattening axis didn't return original scaling");

            yield return null;
        }

        /// <summary>
        /// Test for verifying that rotation handles are properly switched off/on when flattening/ unflattening the rig.
        /// Makes sure rig and handles are not recreated on changing flattening mode.
        /// </summary>
        [UnityTest]
        public IEnumerator RotationHandleFlattenTest()
        {
            // test flatten mode of rotation handle
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);

            // cache rig root for verifying that we're not recreating the rig on config changes
            GameObject rigRoot = boundsControl.transform.Find("rigRoot").gameObject;
            Assert.IsNotNull(rigRoot, "rigRoot couldn't be found");

            // get rotation handle and make sure it's active per default
            Transform rotationHandle = rigRoot.transform.Find("midpoint_2");
            Assert.IsNotNull(rotationHandle, "couldn't find rotation handle");
            Assert.IsTrue(rotationHandle.gameObject.activeSelf, "rotation handle idx 2 wasn't enabled by default");

            // flatten x axis and make sure handle gets deactivated
            boundsControl.FlattenAxis = FlattenModeType.FlattenX;
            Assert.IsFalse(rotationHandle.gameObject.activeSelf, "rotation handle idx 2 wasn't disabled when control was flattened in X axis");
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");

            // unflatten the control again and make sure handle gets activated accordingly
            boundsControl.FlattenAxis = FlattenModeType.DoNotFlatten;
            Assert.IsTrue(rotationHandle.gameObject.activeSelf, "rotation handle idx 2 wasn't enabled on unflatten");
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");

            yield return null;
        }

        /// <summary>
        /// Test for verifying changing the handle prefabs during runtime 
        /// and making sure the entire rig won't be recreated
        /// </summary>
        [UnityTest]
        public IEnumerator RotationHandlePrefabTest()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);
            GameObject childBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var sharedMeshFilter = childBox.GetComponent<MeshFilter>();

            // cache rig root for verifying that we're not recreating the rig on config changes
            GameObject rigRoot = boundsControl.transform.Find("rigRoot").gameObject;
            Assert.IsNotNull(rigRoot, "rigRoot couldn't be found");

            // check default mesh filter
            Transform rotationHandleVisual = rigRoot.transform.Find("midpoint_2/visuals");
            Transform cornerVisual = rigRoot.transform.Find("corner_3/visualsScale/visuals");
            Assert.IsNotNull(rotationHandleVisual, "couldn't find rotation handle visual");
            Assert.IsNotNull(cornerVisual, "couldn't find scale handle visual");
            var handleVisualMeshFilter = rotationHandleVisual.GetComponent<MeshFilter>();

            Assert.IsTrue(handleVisualMeshFilter.mesh.name == "Sphere Instance", "Rotation handles weren't created with default sphere");

            // change mesh
            RotationHandlesConfiguration rotationHandlesConfig = boundsControl.RotationHandlesConfig;
            rotationHandlesConfig.HandlePrefab = childBox;
            yield return null;
            yield return new WaitForFixedUpdate();

            // make sure only the visuals have been destroyed but not the rig root
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNotNull(cornerVisual, "scale handle got destroyed while replacing prefab for rotation handle");
            Assert.IsNull(rotationHandleVisual, "corner visual wasn't destroyed when swapping the prefab");

            // fetch new rotation handle visual
            rotationHandleVisual = rigRoot.transform.Find("midpoint_2/visuals");
            Assert.IsNotNull(rotationHandleVisual, "couldn't find rotation handle visual");
            handleVisualMeshFilter = rotationHandleVisual.GetComponent<MeshFilter>();

            // check if new mesh filter was applied
            Assert.IsTrue(sharedMeshFilter.mesh.name == handleVisualMeshFilter.mesh.name, "box rotation handle wasn't applied");

            yield return null;
        }

        /// <summary>
        /// Test for verifying changing the handle prefabs during runtime 
        /// in regular and flatten mode and making sure the entire rig won't be recreated
        /// </summary>
        [UnityTest]
        public IEnumerator ScaleHandlePrefabTest()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);
            GameObject childSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var sharedMeshFilter = childSphere.GetComponent<MeshFilter>();
            // cache rig root for verifying that we're not recreating the rig on config changes
            GameObject rigRoot = boundsControl.transform.Find("rigRoot").gameObject;
            Assert.IsNotNull(rigRoot, "rigRoot couldn't be found");

            // check default mesh filter
            Transform cornerVisual = rigRoot.transform.Find("corner_3/visualsScale/visuals");
            Assert.IsNotNull(cornerVisual, "couldn't find corner visual");
            Transform rotationHandleVisual = rigRoot.transform.Find("midpoint_2/visuals");
            Assert.IsNotNull(rotationHandleVisual, "couldn't find rotation handle visual");
            var cornerMeshFilter = cornerVisual.GetComponent<MeshFilter>();

            Assert.IsTrue(cornerMeshFilter.mesh.name == "Cube Instance", "Scale handles weren't created with default cube");

            // change mesh
            ScaleHandlesConfiguration scaleHandleConfig = boundsControl.ScaleHandlesConfig;
            scaleHandleConfig.HandlePrefab = childSphere;
            yield return null;
            yield return new WaitForFixedUpdate();

            // make sure only the visuals have been destroyed but not the rig root
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNotNull(rotationHandleVisual, "rotation handle visual got destroyed while replacing the scale handle");
            Assert.IsNull(cornerVisual, "corner visual wasn't destroyed when swapping the prefab");

            // fetch new corner visual
            cornerVisual = rigRoot.transform.Find("corner_3/visualsScale/visuals");
            Assert.IsNotNull(cornerVisual, "couldn't find corner visual");
            cornerMeshFilter = cornerVisual.GetComponent<MeshFilter>();
            // check if new mesh filter was applied
            Assert.IsTrue(sharedMeshFilter.mesh.name == cornerMeshFilter.mesh.name, "sphere scale handle wasn't applied");

            // set flatten mode
            boundsControl.FlattenAxis = FlattenModeType.FlattenX;
            yield return null;
            yield return new WaitForFixedUpdate();

            // make sure only the visuals have been destroyed but not the rig root
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNull(cornerVisual, "corner visual wasn't destroyed when swapping the prefab");

            // mesh should be cube again
            cornerVisual = rigRoot.transform.Find("corner_3/visualsScale/visuals");
            Assert.IsNotNull(cornerVisual, "couldn't find corner visual");
            cornerMeshFilter = cornerVisual.GetComponent<MeshFilter>();
            Assert.IsTrue(cornerMeshFilter.mesh.name == "Cube Instance", "Flattened scale handles weren't created with default cube");
            // reset flatten mode
            boundsControl.FlattenAxis = FlattenModeType.DoNotFlatten;

            scaleHandleConfig.HandleSlatePrefab = childSphere;
            yield return null;
            yield return new WaitForFixedUpdate();

            // make sure only the visuals have been destroyed but not the rig root
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNull(cornerVisual, "corner visual wasn't destroyed when swapping the prefab");

            // fetch new corner visual
            cornerVisual = rigRoot.transform.Find("corner_3/visualsScale/visuals");
            Assert.IsNotNull(cornerVisual, "couldn't find corner visual");
            cornerMeshFilter = cornerVisual.GetComponent<MeshFilter>();

            // check if new mesh filter was applied
            Assert.IsTrue(cornerMeshFilter.mesh.name.StartsWith(sharedMeshFilter.mesh.name), "sphere scale handle wasn't applied");
            yield return null;
        }

        /// <summary>
        /// Tests runtime configuration of scale handle materials.
        /// Verifies scale handle default and grabbed material are properly replaced in all visuals when 
        /// setting the material in the config as well as validating that neither the rig nor the visuals get recreated.
        /// </summary>
        [UnityTest]
        public IEnumerator ScaleHandleMaterialTest()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);
            yield return HandleMaterialTest("corner_3/visualsScale/visuals", boundsControl.ScaleHandlesConfig, boundsControl);
        }

        /// <summary>
        /// Tests runtime configuration of rotation handle materials.
        /// Verifies rotation handle default and grabbed material are properly replaced in all visuals when 
        /// setting the material in the config as well as validating that neither the rig nor the visuals get recreated.
        /// </summary>
        [UnityTest]
        public IEnumerator RotationHandleMaterialTest()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);
            yield return HandleMaterialTest("midpoint_2/visuals", boundsControl.RotationHandlesConfig, boundsControl);
        }

        private IEnumerator HandleMaterialTest(string handleVisualName, HandlesBaseConfiguration handleConfig, BoundsControl boundsControl)
        {
            // fetch rigroot, corner visual and rotation handle config
            GameObject rigRoot = boundsControl.transform.Find("rigRoot").gameObject;
            Assert.IsNotNull(rigRoot, "rigRoot couldn't be found");
            Transform cornerVisual = rigRoot.transform.Find(handleVisualName);
            Assert.IsNotNull(cornerVisual, "couldn't find corner visual");

            // set materials and make sure rig root and visuals haven't been destroyed while doing so
            handleConfig.HandleMaterial = testMaterial;
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            handleConfig.HandleGrabbedMaterial = testMaterialGrabbed;
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNotNull(cornerVisual, "corner visual got destroyed when setting material");
            // make sure color changed on visual
            Assert.AreEqual(cornerVisual.GetComponent<Renderer>().material.color, testMaterial.color, "handle material wasn't applied to visual");

            // grab handle and make sure grabbed material is applied
            var frontRightCornerPos = cornerVisual.position;
            TestHand hand = new TestHand(Handedness.Right);
            yield return hand.Show(Vector3.zero);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.OpenSteadyGrabPoint);
            yield return hand.MoveTo(frontRightCornerPos);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Pinch);
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(cornerVisual.GetComponent<Renderer>().material.color, testMaterialGrabbed.color, "handle grabbed material wasn't applied to visual");
            // release handle
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.OpenSteadyGrabPoint);
        }

        /// <summary>
        /// Tests runtime configuration of scale handle size.
        /// Verifies scale handles are scaled according to new size value without recreating the visual or the rig
        /// </summary>
        [UnityTest]
        public IEnumerator ScaleHandleSizeTest()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);
            yield return HandleSizeTest("corner_3/visualsScale/visuals", boundsControl.ScaleHandlesConfig, boundsControl);
        }

        /// <summary>
        /// Tests runtime configuration of rotation handle size.
        /// Verifies rotation handles are scaled according to new size value without recreating the visual or the rig
        /// </summary>
        [UnityTest]
        public IEnumerator RotationHandleSizeTest()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);
            yield return HandleSizeTest("midpoint_2/visuals", boundsControl.RotationHandlesConfig, boundsControl);
        }

        private IEnumerator HandleSizeTest(string handleVisualName, HandlesBaseConfiguration handleConfig, BoundsControl boundsControl)
        {
            // fetch rigroot, corner visual and rotation handle config
            GameObject rigRoot = boundsControl.transform.Find("rigRoot").gameObject;
            Assert.IsNotNull(rigRoot, "rigRoot couldn't be found");
            Transform handleVisual = rigRoot.transform.Find(handleVisualName);
            Assert.IsNotNull(handleVisual, "couldn't find visual " + handleVisualName);

            // test hand setup
            TestHand hand = new TestHand(Handedness.Right);
            yield return hand.Show(Vector3.zero);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.OpenSteadyGrabPoint);

            // set test materials so we know if we're interacting with the handle later in the test
            handleConfig.HandleMaterial = testMaterial;
            handleConfig.HandleGrabbedMaterial = testMaterialGrabbed;

            // test runtime handle size configuration
            handleConfig.HandleSize = 0.1f;
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNotNull(handleVisual, "visual got destroyed when setting material");
            yield return hand.MoveTo(handleVisual.position + new Vector3(1.0f, 0.0f, 0.0f) * handleConfig.HandleSize * 0.5f);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Pinch);
            Assert.AreEqual(handleVisual.GetComponent<Renderer>().material.color, testMaterialGrabbed.color, "handle wasn't grabbed");
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.OpenSteadyGrabPoint);
        }

        /// <summary>
        /// Tests runtime configuration of scale handle collider padding.
        /// Verifies collider of scale handles are scaled according to new size value 
        /// without recreating the visual or the rig
        /// </summary>
        [UnityTest]
        public IEnumerator ScaleHandleColliderPaddingTest()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);
            yield return HandleColliderPaddingTest("corner_3", "corner_3/visualsScale/visuals", boundsControl.ScaleHandlesConfig, boundsControl);
        }

        /// <summary>
        /// Tests runtime configuration of rotation handle collider padding.
        /// Verifies collider of rotation handles are scaled according to new size value 
        /// without recreating the visual or the rig
        /// </summary>
        [UnityTest]
        public IEnumerator RotationHandleColliderPaddingTest()
        {
            var boundsControl = InstantiateSceneAndDefaultBoundsControl();
            yield return VerifyInitialBoundsCorrect(boundsControl);
            yield return HandleColliderPaddingTest("midpoint_2", "midpoint_2/visuals", boundsControl.RotationHandlesConfig, boundsControl);
        }

        private IEnumerator HandleColliderPaddingTest(string handleName, string handleVisualName, HandlesBaseConfiguration handleConfig, BoundsControl boundsControl)
        {
            // fetch rigroot, corner visual and rotation handle config
            GameObject rigRoot = boundsControl.transform.Find("rigRoot").gameObject;
            Assert.IsNotNull(rigRoot, "rigRoot couldn't be found");
            Transform cornerVisual = rigRoot.transform.Find(handleVisualName);
            Assert.IsNotNull(cornerVisual, "couldn't find visual" + handleVisualName);
            // init test hand
            TestHand hand = new TestHand(Handedness.Right);
            yield return hand.Show(Vector3.zero);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.OpenSteadyGrabPoint);

            // set test materials so we know if we're interacting with the handle later in the test
            handleConfig.HandleMaterial = testMaterial;
            handleConfig.HandleGrabbedMaterial = testMaterialGrabbed;
            yield return new WaitForFixedUpdate();

            // move hand to edge of rotation handle collider
            Transform cornerHandle = rigRoot.transform.Find(handleName);
            var cornerCollider = cornerHandle.GetComponent<BoxCollider>();
            Vector3 originalColliderExtents = cornerCollider.bounds.extents;

            yield return hand.MoveTo(cornerHandle.position + originalColliderExtents);
            // test runtime collider padding configuration
            Vector3 newColliderPadding = handleConfig.ColliderPadding * 5.0f;

            // move hand to new collider bounds edge before setting the new value in the config
            yield return hand.MoveTo(cornerHandle.position + originalColliderExtents + (newColliderPadding * 0.5f));
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Pinch);
            // handle shouldn't be in grabbed state
            Assert.AreEqual(cornerVisual.GetComponent<Renderer>().material.color, testMaterial.color, "handle was grabbed outside collider padding area");

            yield return hand.SetGesture(ArticulatedHandPose.GestureId.OpenSteadyGrabPoint);
            // now adjust collider bounds and try grabbing the handle again
            handleConfig.ColliderPadding = handleConfig.ColliderPadding + newColliderPadding;
            yield return new WaitForFixedUpdate();
            Assert.IsNotNull(rigRoot, "rigRoot got destroyed while configuring bounds control during runtime");
            Assert.IsNotNull(cornerVisual, "corner visual got destroyed when setting material");
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Pinch);
            // handle should be grabbed now
            Assert.AreEqual(cornerVisual.GetComponent<Renderer>().material.color, testMaterialGrabbed.color, "handle wasn't grabbed");
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.OpenSteadyGrabPoint);
        }

        /// <summary>
        /// Test starting and ending manipulating an object via the app bar
        /// </summary>
        [UnityTest]
        public IEnumerator ManipulateViaAppBarFarInteraction()
        {
            // create cube with bounds control and app bar
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = boundsControlStartCenter;
            BoundsControl boundsControl = cube.AddComponent<BoundsControl>();

            TestUtilities.PlayspaceToOriginLookingForward();

            boundsControl.transform.localScale = boundsControlStartScale;
            Object appBarPrefab = AssetDatabase.LoadAssetAtPath(appBarPrefabLink, typeof(Object));
            Assert.IsNotNull(appBarPrefab, "Couldn't load app bar prefab from assetdatabase");
            GameObject appBarGameObject = Object.Instantiate(appBarPrefab) as GameObject;
            Assert.IsNotNull(appBarGameObject, "Couldn't instantiate appbar prefab");
            appBarGameObject.SetActive(false);
            AppBar appBar = appBarGameObject.GetComponent<AppBar>();
            Assert.IsNotNull(appBar, "Couldn't find AppBar component in prefab");

            appBarGameObject.transform.localScale = Vector3.one * 5.0f;
            appBar.Target = boundsControl;
            appBarGameObject.SetActive(true);

            // manipulation coords
            Vector3 rightCornerInteractionPoint = new Vector3(0.184f, 0.078f, 0.79f); // position of hand for far interacting with front right corner 
            Vector3 pointOnCube = new Vector3(-0.033f, -0.129f, 0.499f); // position where hand ray points on center of the test cube
            Vector3 scalePoint = new Vector3(0.165f, 0.267f, 0.794f); // end position for far interaction scaling
            Vector3 appBarButtonStart = new Vector3(-0.028f, -0.263f, 0.499f); // location of hand for interaction with the app bar manipulation button after scene setup
            Vector3 appBarButtonAfterScale = new Vector3(0.009f, -0.255f, 0.499f); // location of the hand for interaction with the app bar manipulation button after scaling

            // first test to interact with the cube without activating the app bar
            // this shouldn't scale the cube
            TestHand hand = new TestHand(Handedness.Left);
            yield return hand.Show(pointOnCube); // Initially make sure that hand ray is pointed on cube surface so we won't go behind the cube with our ray
            yield return hand.MoveTo(rightCornerInteractionPoint);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Pinch);
            yield return hand.MoveTo(scalePoint);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Open);
            var endBounds = boundsControl.GetComponent<BoxCollider>().bounds;
            TestUtilities.AssertAboutEqual(endBounds.center, boundsControlStartCenter, "endBounds incorrect center");
            TestUtilities.AssertAboutEqual(endBounds.size, boundsControlStartScale, "endBounds incorrect size");

            // now activate the bounds control via app bar
            yield return hand.MoveTo(appBarButtonStart);
            yield return hand.Click();

            // check if we can scale the box now
            yield return hand.MoveTo(pointOnCube); // make sure our hand ray is on the cube again before moving to the scale corner
            yield return hand.MoveTo(rightCornerInteractionPoint); // move to scale corner
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Pinch);
            yield return hand.MoveTo(scalePoint);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Open);
            endBounds = boundsControl.GetComponent<BoxCollider>().bounds;
            Vector3 expectedScaleCenter = new Vector3(0.0453f, 0.0453f, 1.455f);
            Vector3 expectedScaleSize = Vector3.one * 0.59f;
            TestUtilities.AssertAboutEqual(endBounds.center, expectedScaleCenter, "endBounds incorrect center");
            TestUtilities.AssertAboutEqual(endBounds.size, expectedScaleSize, "endBounds incorrect size");

            // deactivate the bounds control via app bar
            yield return hand.MoveTo(appBarButtonAfterScale);
            yield return hand.Click();

            // check if we can scale the box - box shouldn't scale
            Vector3 startLocationScaleToOriginal = new Vector3(0.181f, 0.013f, 0.499f);
            Vector3 endLocationScaleToOriginal = new Vector3(0.121f, -0.052f, 0.499f);
            yield return hand.MoveTo(pointOnCube); // make sure our hand ray is on the cube again before moving to the scale corner
            yield return hand.MoveTo(startLocationScaleToOriginal); // move to scale corner
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Pinch);
            yield return hand.MoveTo(endLocationScaleToOriginal);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Open);
            endBounds = boundsControl.GetComponent<BoxCollider>().bounds;
            TestUtilities.AssertAboutEqual(endBounds.center, expectedScaleCenter, "endBounds incorrect center");
            TestUtilities.AssertAboutEqual(endBounds.size, expectedScaleSize, "endBounds incorrect size");

            // activate the bounds control via app bar
            yield return hand.MoveTo(appBarButtonAfterScale);
            yield return hand.Click();

            // try again to scale the box back
            yield return hand.MoveTo(pointOnCube); // make sure our hand ray is on the cube again before moving to the scale corner
            yield return hand.MoveTo(startLocationScaleToOriginal); // move to scale corner
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Pinch);
            yield return hand.MoveTo(endLocationScaleToOriginal);
            yield return hand.SetGesture(ArticulatedHandPose.GestureId.Open);
            endBounds = boundsControl.GetComponent<BoxCollider>().bounds;
            TestUtilities.AssertAboutEqual(endBounds.center, boundsControlStartCenter, "endBounds incorrect center");
            TestUtilities.AssertAboutEqual(endBounds.size, boundsControlStartScale, "endBounds incorrect size");

            yield return null;
        }

        /// <summary>
        /// Returns the AABB of the bounds control rig (corners, edges)
        /// that make up the bounds control by using the positions of the corners
        /// </summary>
        private Bounds GetBoundsControlRigBounds(BoundsControl boundsControl)
        {
            Bounds b = new Bounds();
            b.center = boundsControl.transform.Find("rigRoot/corner_0").position;
            for (int i = 1; i < 8; ++i)
            {
                Transform corner = boundsControl.transform.Find("rigRoot/corner_" + i.ToString());
                b.Encapsulate(corner.position);
            }
            return b;
        }
    }
}
#endif