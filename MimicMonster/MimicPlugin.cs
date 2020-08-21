using System;
using System.Reflection;
using System.Collections.Generic;
using BepInEx;
using R2API;
using R2API.Utils;
using EntityStates;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;

namespace EnforcerPlugin
{
    [BepInDependency("com.bepis.r2api", BepInDependency.DependencyFlags.HardDependency)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    [BepInPlugin(MODUID, "MimicMonster", "0.0.1")]
    [R2APISubmoduleDependency(new string[]
    {
        "PrefabAPI",
        "LoadoutAPI",
        "LanguageAPI",
        "SurvivorAPI"//remove survivorapi when the test survivor is removed, don't need it
    })]

    public class MimicPlugin : BaseUnityPlugin
    {
        public const string MODUID = "com.rob.MimicMonster";

        public static MimicPlugin instance;

        public static GameObject characterPrefab;

        public static event Action awake;

        public SkillLocator skillLocator;

        public MimicPlugin()
        {
            awake += MimicPlugin_Load;
        }

        private void MimicPlugin_Load()
        {
            Assets.PopulateAssets();
            CreatePrefab();
            RegisterCharacter();
            //ItemDisplays.RegisterDisplays();
        }

        public void Awake()
        {
            Action awake = MimicPlugin.awake;
            if (awake == null)
            {
                return;
            }
            awake();
        }

        private static GameObject CreateModel()
        {
            GameObject model = Assets.MainAssetBundle.LoadAsset<GameObject>("mdlMimic");

            return model;
        }

        private static void CreatePrefab()
        {
            //cloning the mushroom
            characterPrefab = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/CharacterBodies/MiniMushroomBody"), "MimicBody");

            characterPrefab.GetComponent<NetworkIdentity>().localPlayerAuthority = true;

            //instantiate the mimic's model
            GameObject model = CreateModel();

            GameObject modelBase = characterPrefab.transform.Find("ModelBase").gameObject;

            //destroy mdlMushroom
            Destroy(modelBase.transform.GetChild(0).gameObject);

            //resize the model
            modelBase.transform.localScale *= 0.5f;

            //replace it with our own model
            Transform transform = model.transform;
            transform.parent = modelBase.transform;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            //ignore
            CharacterDirection characterDirection = characterPrefab.GetComponent<CharacterDirection>();
            characterDirection.moveVector = Vector3.zero;
            characterDirection.targetTransform = modelBase.transform;
            characterDirection.overrideAnimatorForwardTransform = null;
            characterDirection.rootMotionAccumulator = null;
            characterDirection.modelAnimator = model.GetComponentInChildren<Animator>();
            characterDirection.driveFromRootRotation = false;
            characterDirection.turnSpeed = 720f;

            LanguageAPI.Add("MIMIC_NAME", "Mimic");

            //self explanatory
            CharacterBody bodyComponent = characterPrefab.GetComponent<CharacterBody>();
            bodyComponent.bodyIndex = -1;
            bodyComponent.name = "MimicBody";
            bodyComponent.baseNameToken = "MIMIC_NAME";
            bodyComponent.subtitleNameToken = "NULL_SUBTITLE";
            bodyComponent.rootMotionInMainState = false;
            bodyComponent.mainRootSpeed = 0;
            bodyComponent.baseMaxHealth = 300;
            bodyComponent.levelMaxHealth = 100;
            bodyComponent.baseRegen = 0f;
            bodyComponent.levelRegen = 0f;
            bodyComponent.baseMaxShield = 0;
            bodyComponent.levelMaxShield = 0;
            bodyComponent.baseMoveSpeed = 7;
            bodyComponent.levelMoveSpeed = 0;
            bodyComponent.baseAcceleration = 80;
            bodyComponent.baseJumpPower = 15;
            bodyComponent.levelJumpPower = 0;
            bodyComponent.baseDamage = 12;
            bodyComponent.levelDamage = 2.4f;
            bodyComponent.baseAttackSpeed = 1;
            bodyComponent.levelAttackSpeed = 0;
            bodyComponent.baseCrit = 0;
            bodyComponent.levelCrit = 0;
            bodyComponent.baseArmor = 0;
            bodyComponent.levelArmor = 0;
            bodyComponent.baseJumpCount = 1;
            bodyComponent.sprintingSpeedMultiplier = 1.55f;
            bodyComponent.hideCrosshair = false;
            bodyComponent.crosshairPrefab = Resources.Load<GameObject>("Prefabs/Crosshair/SimpleDotCrosshair");
            bodyComponent.hullClassification = HullClassification.Human;
            bodyComponent.portraitIcon = Assets.charPortrait;
            bodyComponent.isChampion = false;

            //also ignore
            CharacterMotor characterMotor = characterPrefab.GetComponent<CharacterMotor>();
            characterMotor.walkSpeedPenaltyCoefficient = 1f;
            characterMotor.characterDirection = characterDirection;
            characterMotor.muteWalkMotion = false;
            characterMotor.mass = 100f;
            characterMotor.airControl = 0.25f;
            characterMotor.disableAirControlUntilCollision = false;
            characterMotor.generateParametersOnAwake = true;

            //set up the modellocator so stuff can find the model
            ModelLocator modelLocator = characterPrefab.GetComponent<ModelLocator>();
            modelLocator.modelTransform = transform;
            modelLocator.modelBaseTransform = modelBase.transform;

            ChildLocator childLocator = model.GetComponent<ChildLocator>();

            //charactermodel and rendererinfo important for all visual stuff, skins, overlays, etc.
            CharacterModel characterModel = model.AddComponent<CharacterModel>();
            characterModel.body = bodyComponent;
            characterModel.baseRendererInfos = new CharacterModel.RendererInfo[]
            {
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = model.GetComponentInChildren<SkinnedMeshRenderer>().material,
                    renderer = model.GetComponentInChildren<SkinnedMeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                }
            };
            characterModel.autoPopulateLightInfos = true;
            characterModel.invisibilityCount = 0;
            characterModel.temporaryOverlays = new List<TemporaryOverlay>();

            //gotta replace the material with a new one using hotpoo shaders:tm: for maximum quality

            CharacterModel.RendererInfo[] rendererInfos = characterModel.baseRendererInfos;
            CharacterModel.RendererInfo[] array = new CharacterModel.RendererInfo[rendererInfos.Length];
            rendererInfos.CopyTo(array, 0);

            //clone commando material and replace with our own textures
            Material material = array[0].defaultMaterial;

            if (material)
            {
                material = UnityEngine.Object.Instantiate<Material>(Resources.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody").GetComponentInChildren<CharacterModel>().baseRendererInfos[0].defaultMaterial);
                material.SetColor("_Color", Color.white);
                material.SetTexture("_MainTex", Assets.MainAssetBundle.LoadAsset<Material>("matMimic").GetTexture("_MainTex"));
                material.SetColor("_EmColor", Color.white);
                material.SetFloat("_EmPower", 1);
                material.SetTexture("_EmTex", Assets.MainAssetBundle.LoadAsset<Material>("matMimic").GetTexture("_EmissionMap"));
                material.SetFloat("_NormalStrength", 0);

                array[0].defaultMaterial = material;
            }

            //now replace the material on our model
            characterModel.baseRendererInfos = array;

            //mainrenderer is needed for skin stuff
            characterModel.SetFieldValue("mainSkinnedMeshRenderer", characterModel.baseRendererInfos[0].renderer.gameObject.GetComponent<SkinnedMeshRenderer>());

            TeamComponent teamComponent = null;
            if (characterPrefab.GetComponent<TeamComponent>() != null) teamComponent = characterPrefab.GetComponent<TeamComponent>();
            else teamComponent = characterPrefab.GetComponent<TeamComponent>();
            teamComponent.hideAllyCardDisplay = false;
            teamComponent.teamIndex = TeamIndex.None;

            characterPrefab.GetComponent<Interactor>().maxInteractionDistance = 3f;
            characterPrefab.GetComponent<InteractionDriver>().highlightInteractor = true;

            //replace with custom sfx if applicable
            SfxLocator sfxLocator = characterPrefab.GetComponent<SfxLocator>();
            sfxLocator.deathSound = "";
            sfxLocator.barkSound = "";
            sfxLocator.openSound = "";
            sfxLocator.landingSound = "Play_char_land";
            sfxLocator.fallDamageSound = "";
            sfxLocator.aliveLoopStart = "";
            sfxLocator.aliveLoopStop = "";

            Rigidbody rigidbody = characterPrefab.GetComponent<Rigidbody>();
            rigidbody.mass = 100f;
            rigidbody.drag = 0f;
            rigidbody.angularDrag = 0f;
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
            rigidbody.interpolation = RigidbodyInterpolation.None;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rigidbody.constraints = RigidbodyConstraints.None;

            //add the main hurtbox
            HurtBoxGroup hurtBoxGroup = model.AddComponent<HurtBoxGroup>();

            HurtBox mainHurtbox = childLocator.FindChild("MainHurtbox").gameObject.AddComponent<HurtBox>();
            mainHurtbox.gameObject.layer = LayerIndex.entityPrecise.intVal;
            mainHurtbox.healthComponent = characterPrefab.GetComponent<HealthComponent>();
            mainHurtbox.isBullseye = true;
            mainHurtbox.damageModifier = HurtBox.DamageModifier.Normal;
            mainHurtbox.hurtBoxGroup = hurtBoxGroup;
            mainHurtbox.indexInGroup = 0;

            //make a hitbox for the chomp
            HitBoxGroup hitBoxGroup = model.AddComponent<HitBoxGroup>();

            GameObject chompHitbox = childLocator.FindChild("ChompHitbox").gameObject;
            chompHitbox.transform.localScale = new Vector3(8f, 8f, 8f);

            HitBox hitBox = chompHitbox.AddComponent<HitBox>();
            chompHitbox.layer = LayerIndex.projectile.intVal;

            hitBoxGroup.hitBoxes = new HitBox[]
            {
                hitBox
            };

            hitBoxGroup.groupName = "Chomp";

            //footstep sounds because mimics have feet
            FootstepHandler footstepHandler = model.AddComponent<FootstepHandler>();
            footstepHandler.baseFootstepString = "Play_player_footstep";
            footstepHandler.sprintFootstepOverrideString = "";
            footstepHandler.enableFootstepDust = true;
            footstepHandler.footstepDustPrefab = Resources.Load<GameObject>("Prefabs/GenericFootstepDust");

            //it doesn't have aim animations yet but doesn't hurt to include this for the future
            AimAnimator aimAnimator = model.AddComponent<AimAnimator>();
            aimAnimator.directionComponent = characterDirection;
            aimAnimator.pitchRangeMax = 60f;
            aimAnimator.pitchRangeMin = -60f;
            aimAnimator.yawRangeMin = -90f;
            aimAnimator.yawRangeMax = 90f;
            aimAnimator.pitchGiveupRange = 30f;
            aimAnimator.yawGiveupRange = 10f;
            aimAnimator.giveupDuration = 3f;
            aimAnimator.inputBank = characterPrefab.GetComponent<InputBankTest>();
        }

        private void RegisterCharacter()
        {
            SkillSetup();

            //this is just for easy testing, remove it whenever
            SurvivorDef survivorDef = new SurvivorDef
            {
                name = "MIMIC_NAME",
                unlockableName = "",
                descriptionToken = "",
                primaryColor = Color.grey,
                bodyPrefab = characterPrefab,
                displayPrefab = characterPrefab,
                outroFlavorToken = "GENERIC_OUTRO_FLAVOR"
            };
            SurvivorAPI.AddSurvivor(survivorDef);
            //

            BodyCatalog.getAdditionalEntries += delegate (List<GameObject> list)
            {
                list.Add(characterPrefab);
            };
        }

        private void SkillSetup()
        {
            foreach (GenericSkill obj in characterPrefab.GetComponentsInChildren<GenericSkill>())
            {
                BaseUnityPlugin.DestroyImmediate(obj);
            }

            skillLocator = characterPrefab.GetComponent<SkillLocator>();

            PrimarySetup();
            UtilitySetup();
        }

        private void PrimarySetup()
        {
            //register whatever skillstates are being used
            //LoadoutAPI.AddSkill(typeof());

            SkillDef mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(EntityStates.LemurianMonster.ChargeFireball));
            mySkillDef.activationStateMachineName = "Body";
            mySkillDef.baseMaxStock = 1;
            mySkillDef.baseRechargeInterval = 0f;
            mySkillDef.beginSkillCooldownOnSkillEnd = false;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.Any;
            mySkillDef.isBullets = false;
            mySkillDef.isCombatSkill = true;
            mySkillDef.mustKeyPress = false;
            mySkillDef.noSprint = true;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.shootDelay = 0f;
            mySkillDef.stockToConsume = 1;

            LoadoutAPI.AddSkillDef(mySkillDef);

            skillLocator.primary = characterPrefab.AddComponent<GenericSkill>();
            SkillFamily newFamily = ScriptableObject.CreateInstance<SkillFamily>();
            newFamily.variants = new SkillFamily.Variant[1];
            LoadoutAPI.AddSkillFamily(newFamily);
            skillLocator.primary.SetFieldValue("_skillFamily", newFamily);
            SkillFamily skillFamily = skillLocator.primary.skillFamily;

            skillFamily.variants[0] = new SkillFamily.Variant
            {
                skillDef = mySkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(mySkillDef.skillNameToken, false, null)
            };
        }

        private void UtilitySetup()
        {
            //LoadoutAPI.AddSkill(typeof());

            SkillDef mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(EntityStates.Croco.ChainableLeap));
            mySkillDef.activationStateMachineName = "Body";
            mySkillDef.baseMaxStock = 1;
            mySkillDef.baseRechargeInterval = 16f;
            mySkillDef.beginSkillCooldownOnSkillEnd = false;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.Any;
            mySkillDef.isBullets = false;
            mySkillDef.isCombatSkill = true;
            mySkillDef.mustKeyPress = false;
            mySkillDef.noSprint = true;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.shootDelay = 0f;
            mySkillDef.stockToConsume = 1;

            LoadoutAPI.AddSkillDef(mySkillDef);

            skillLocator.utility = characterPrefab.AddComponent<GenericSkill>();
            SkillFamily newFamily = ScriptableObject.CreateInstance<SkillFamily>();
            newFamily.variants = new SkillFamily.Variant[1];
            LoadoutAPI.AddSkillFamily(newFamily);
            skillLocator.utility.SetFieldValue("_skillFamily", newFamily);
            SkillFamily skillFamily = skillLocator.utility.skillFamily;

            skillFamily.variants[0] = new SkillFamily.Variant
            {
                skillDef = mySkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(mySkillDef.skillNameToken, false, null)
            };
        }
    }

    public static class Assets
    {
        public static AssetBundle MainAssetBundle = null;

        public static Texture charPortrait;

        public static void PopulateAssets()
        {
            // asset loading stuff zz
            if (MainAssetBundle == null)
            {
                using (var assetStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MimicMonster.mimic"))
                {
                    MainAssetBundle = AssetBundle.LoadFromStream(assetStream);
                }
            }

            /*using (Stream manifestResourceStream2 = Assembly.GetExecutingAssembly().GetManifestResourceStream("MimicMonster.MimicBank.bnk"))
            {
                byte[] array = new byte[manifestResourceStream2.Length];
                manifestResourceStream2.Read(array, 0, array.Length);
                SoundAPI.SoundBanks.Add(array);
            }*/

            // didn't make a portrait yet
            //charPortrait = MainAssetBundle.LoadAsset<Sprite>("MimicBody").texture;
        }
    }
}