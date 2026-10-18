using UnityEngine;
using CozyFarm.Core;
using CozyFarm.Farming;

namespace CozyFarm.Player
{
    public class FarmerController : MonoBehaviour
    {
        private static FarmerController instance;
        public static FarmerController Instance
        {
            get
            {
                if (instance == null)
                    instance = FindFirstObjectByType<FarmerController>();
                return instance;
            }
            private set { instance = value; }
        }

        [Header("Movement & Camera")]
        public float moveSpeed = 3.8f;
        public float turnSpeed = 12f;
        public Transform cameraRig;
        public Vector3 cameraOffset = new Vector3(0f, 11.8f, -9.6f);
        public Vector3 cameraLookOffset = new Vector3(0f, 0.6f, 1.4f);

        [Header("Visuals & Tools")]
        public Transform visualRoot;
        public GameObject wateringCanTool;
        public GameObject hoeTool;
        public ParticleSystem waterSprayParticles;

        [Header("Market Zone")]
        public Vector3 marketPlazaPosition = new Vector3(5.8f, 0f, 1.8f);
        public float marketSellRadius = 2.6f;

        private Vector3? targetMovePosition = null;
        private float walkBobPhase = 0f;
        private float waterSprayTimer = 0f;

        private void Awake()
        {
            instance = this;
        }

        private void Update()
        {
            HandleKeyboardHotbar();
            HandleMovement(Time.deltaTime);
            HandleActionTimers(Time.deltaTime);
            UpdateCameraFollow(Time.deltaTime);
        }

        private void HandleKeyboardHotbar()
        {
            if (CozyFarmState.Instance == null) return;

            if (Input.GetKeyDown(KeyCode.Alpha1)) SetSelectedSlot(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetSelectedSlot(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetSelectedSlot(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetSelectedSlot(3);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SetSelectedSlot(4);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SetSelectedSlot(5);

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))
            {
                PerformContextActionAtCurrentPosition();
            }
        }

        public void SetSelectedSlot(int slotIndex)
        {
            if (CozyFarmState.Instance == null) return;
            CozyFarmState.Instance.selectedHotbarSlot = Mathf.Clamp(slotIndex, 0, 5);
            if (wateringCanTool != null)
                wateringCanTool.SetActive(slotIndex == 0);
            if (hoeTool != null)
                hoeTool.SetActive(slotIndex == 1);
        }

        public void MoveToPoint(Vector3 worldTarget)
        {
            worldTarget.y = 0f;
            targetMovePosition = worldTarget;
        }

        public bool HasReachedTarget(float tolerance = 0.25f)
        {
            if (!targetMovePosition.HasValue) return true;
            Vector3 flatDelta = targetMovePosition.Value - transform.position;
            flatDelta.y = 0f;
            return flatDelta.sqrMagnitude <= tolerance * tolerance;
        }

        private void HandleMovement(float dt)
        {
            Vector3 inputDir = Vector3.zero;
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
            {
                inputDir = new Vector3(h, 0f, v).normalized;
                targetMovePosition = null;
            }
            else if (targetMovePosition.HasValue)
            {
                Vector3 toTarget = targetMovePosition.Value - transform.position;
                toTarget.y = 0f;
                if (toTarget.magnitude > 0.14f)
                {
                    inputDir = toTarget.normalized;
                }
                else
                {
                    transform.position = new Vector3(targetMovePosition.Value.x, 0f, targetMovePosition.Value.z);
                    targetMovePosition = null;
                }
            }

            if (inputDir.sqrMagnitude > 0.001f)
            {
                Vector3 nextPos = transform.position + inputDir * (moveSpeed * dt);
                nextPos.x = Mathf.Clamp(nextPos.x, -9.5f, 10.5f);
                nextPos.z = Mathf.Clamp(nextPos.z, -8.5f, 8.5f);
                nextPos.y = 0f;
                transform.position = nextPos;

                Quaternion targetRot = Quaternion.LookRotation(inputDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * dt);

                walkBobPhase += dt * 11f;
                if (visualRoot != null)
                {
                    float bobY = Mathf.Abs(Mathf.Sin(walkBobPhase)) * 0.07f;
                    float swayZ = Mathf.Sin(walkBobPhase) * 4.5f;
                    visualRoot.localPosition = new Vector3(0f, bobY, 0f);
                    visualRoot.localRotation = Quaternion.Euler(0f, 0f, swayZ);
                }
            }
            else if (visualRoot != null)
            {
                visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, Vector3.zero, 10f * dt);
                visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, Quaternion.identity, 10f * dt);
            }

            // Auto-sell when walking onto the Market Stall plaza with harvested produce
            if (CozyFarmState.Instance != null)
            {
                Vector3 dMarket = transform.position - marketPlazaPosition;
                dMarket.y = 0f;
                if (dMarket.magnitude <= marketSellRadius &&
                    (CozyFarmState.Instance.harvestedPumpkinCount > 0 || CozyFarmState.Instance.harvestedTomatoCount > 0))
                {
                    CozyFarmState.Instance.SellAllHarvestedProduce();
                }
            }
        }

        private void HandleActionTimers(float dt)
        {
            if (waterSprayTimer > 0f)
            {
                waterSprayTimer -= dt;
                if (waterSprayParticles != null && !waterSprayParticles.isPlaying)
                {
                    waterSprayParticles.Play();
                }
            }
            else if (waterSprayParticles != null && waterSprayParticles.isPlaying)
            {
                waterSprayParticles.Stop();
            }
        }

        public bool PerformContextActionAtCurrentPosition()
        {
            if (FarmPlotGrid.Instance == null || CozyFarmState.Instance == null)
                return false;

            CropSlot slot = FarmPlotGrid.Instance.FindNearestSlot(transform.position, 1.55f);
            if (slot == null)
                return false;

            return InteractWithSlot(slot);
        }

        public bool InteractWithSlot(CropSlot slot)
        {
            if (slot == null || CozyFarmState.Instance == null)
                return false;

            // 1. If mature, harvest immediately!
            if (slot.stage == CropStage.Mature)
            {
                SetSelectedSlot(1); // Show hoe/harvest tool briefly
                CropType harvested = slot.Harvest();
                if (harvested == CropType.Pumpkin)
                    CozyFarmState.Instance.harvestedPumpkinCount++;
                else if (harvested == CropType.Tomato)
                    CozyFarmState.Instance.harvestedTomatoCount++;

                CozyFarmState.Instance.totalHarvested++;
                return true;
            }

            // 2. If empty, plant seed!
            if (slot.stage == CropStage.Empty)
            {
                CropType toPlant = CozyFarmState.Instance.selectedHotbarSlot == 3
                    ? CropType.Tomato
                    : CropType.Pumpkin;

                if (slot.Plant(toPlant))
                {
                    CozyFarmState.Instance.totalPlanted++;
                    // Also water right away if using watering can
                    if (CozyFarmState.Instance.selectedHotbarSlot == 0)
                    {
                        TriggerWateringOnSlot(slot);
                    }
                    return true;
                }
            }

            // 3. Water seeded/sprout slot
            if (!slot.isWatered)
            {
                TriggerWateringOnSlot(slot);
                return true;
            }

            return false;
        }

        public void TriggerWateringOnSlot(CropSlot slot)
        {
            if (slot == null) return;
            SetSelectedSlot(0);
            if (slot.Water() && CozyFarmState.Instance != null)
            {
                CozyFarmState.Instance.totalWatered++;
            }
            waterSprayTimer = 0.65f;
        }

        private void UpdateCameraFollow(float dt)
        {
            if (cameraRig == null) return;
            // Subtle diorama follow keeping the farm island beautifully framed
            Vector3 focus = new Vector3(
                Mathf.Clamp(transform.position.x * 0.55f, -2.5f, 3.6f),
                0f,
                Mathf.Clamp(transform.position.z * 0.45f, -2.2f, 2.2f)
            );
            Vector3 desiredPos = focus + cameraOffset;
            cameraRig.position = Vector3.Lerp(cameraRig.position, desiredPos, 5.5f * dt);
            cameraRig.LookAt(focus + cameraLookOffset);
        }
    }
}
