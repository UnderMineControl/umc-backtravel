//Most of this is a straight RIP from BWDY's BACKTRAVEL mod (https://github.com/bwdymods/UM-BackTravel)
using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;

namespace UnderMineControl.BackTravel
{
    using API;
    using FMODUnity;
    using Thor;
    using UnityEngine;

    public class BackTravel : IMod
    {
        private static IPatcher _patcher;
        private static IGame _game;
        private static API.ILogger _logger;
        private static IEvents _events;

        private static bool _showAll = false;

        public BackTravel(IPatcher patcher, IGame game, API.ILogger logger, IEvents events)
        {
            _game = game;
            _patcher = patcher;
            _logger = logger;
            _events = events;
        }

        public void Initialize()
        {
            _patcher.Patch(this, typeof(WarpPopup), "Initialize", "WarpPopup", null, typeof(object), typeof(Entity));

            _events.OnGameUpdated += (a, b) =>
            {
                if (_game.KeyDown(KeyCode.Keypad1))
                {
                    _showAll = !_showAll;
                    _logger.Debug("Showing All Maps: " + _showAll);
                }
            };
        }


        public static bool WarpPopup(WarpPopup __instance, object data, Entity owner)
        {
            _logger.Info("Intercepting warp menu to allow back-travel...");

            //snag some private fields via reflection helper
            var mListItems = GetField<List<WarpListItem>>(__instance, "mListItems");
            var m_itemPrefab = GetField<WarpListItem>(__instance, "m_itemPrefab");
            var m_container = GetField<RadialLayoutGroup>(__instance, "m_container");
            var m_content = GetField<GameObject>(__instance, "m_content");
            var m_reminder = GetField<GameObject>(__instance, "m_reminder");
            var m_maps = GetField<List<UpgradeData>>(__instance, "m_maps");
            //we are replacing the Initialize method, so let's do the variable initialization

            BaseInitialization(__instance, data, owner);

            _logger.Debug("Intialized.");

            if (__instance.RectTransform.anchoredPosition.y < -200.0)
                __instance.Animator.SetInteger("state", 1);

            m_container.transform.DORotate(Vector3.forward * 360f, 0.25f, RotateMode.FastBeyond360);

            DOTween.To
            (
                () => m_container.Radius, 
                x => m_container.Radius = x, 
                m_container.Radius,
                0.25f
            ).OnComplete(() => SetProperty(__instance, "Ready", true));

            m_container.Radius = 0.0f;
            WarpListItem warpListItem = null;

            _logger.Debug("Setting up map");

            
            //let's choose our destinations
            foreach (var map in m_maps)
            {
                if ((map.IsDiscovered && (map.UserData == -1 || map.UserData > Game.Instance.Simulation.Zone.Data.ZoneNumber)) ||
                    (map.IsDiscovered && map.UserData < Game.Instance.Simulation.Zone.Data.ZoneNumber) ||
                    (_showAll))
                {
                    warpListItem = warpListItem == null ? m_itemPrefab : Object.Instantiate(m_itemPrefab, m_container.transform);
                    warpListItem.Initialize(owner?.PlayerID ?? 0, map);
                    mListItems.Add(warpListItem);
                }
            }


            //do we have anywhere to go? handle accordingly.
            if (mListItems.Count > 0)
            {
                _logger.Debug("We have a place");
                m_content.SetActive(value: true);
                m_reminder.SetActive(value: false);
            }
            else
            {
                _logger.Debug("we don't have a place");
                m_content.SetActive(value: false);
                m_reminder.SetActive(value: true);
                Object.Destroy(m_itemPrefab.gameObject);
            }

            //hook up the closed event delegate (these things are tricky in reflection)
            var m = typeof(WarpPopup).GetMethod("OnClosed", BindingFlags.NonPublic | BindingFlags.Instance);
            __instance.RegisterEvent(UIEvent.EventType.Closed, (Popup.EventHandler)Delegate.CreateDelegate(typeof(Popup.EventHandler), __instance, m));
            _logger.Debug("Finished");
            //return false to stop the original from running
            return false;

        }

        public static void BaseInitialization(WarpPopup __instance, object data, Entity owner)
        {
            SetField(__instance, "mTimer", 0f, typeof(Popup));
            SetField(__instance, "mData", data, typeof(Popup));
            SetField(__instance, "mOwner", owner, typeof(Popup));
            SetField(__instance, "mPlayerID", ((owner != null) ? owner.PlayerID : 0), typeof(Popup));

            __instance.Animator.Update(Time.deltaTime);
            __instance.CanvasGroup.interactable = __instance.CanvasGroup.blocksRaycasts = true;

            if (__instance.InputContext != null)
                Game.Instance.InputSystem.AddContext(_game.Player.ID, __instance.InputContext);

            Invoke(__instance, "Layout");
            RuntimeManager.PlayOneShot(GetField<string>(__instance, "m_openAudio", typeof(Popup)), new Vector3());
        }

        #region Reflector
        private const BindingFlags BindingFlagsAll = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        public static T GetField<T>(object instance, string name, Type type = null)
        {
            Type t = (type == null ? instance.GetType() : type);
            return (T)t.GetField(name, BindingFlagsAll).GetValue(instance);
        }

        public static void SetField(object instance, string name, object value, Type type = null)
        {
            Type t = (type == null ? instance.GetType() : type);
            t.GetField(name, BindingFlagsAll).SetValue(instance, value);
        }

        public static T GetProperty<T>(object instance, string name, Type type = null)
        {
            Type t = (type == null ? instance.GetType() : type);
            return (T)t.GetProperty(name, BindingFlagsAll).GetValue(instance);
        }

        public static void SetProperty(object instance, string name, object value, Type type = null)
        {
            Type t = (type == null ? instance.GetType() : type);
            t.GetProperty(name, BindingFlagsAll).SetValue(instance, value);
        }

        //parameterless invocation with return
        public static T Invoke<T>(object instance, string name, Type type = null)
        {
            Type t = (type == null ? instance.GetType() : type);
            return (T)t.GetMethod(name, BindingFlagsAll).Invoke(instance, null);
        }

        //parameterless invocation without return (void)
        public static void Invoke(object instance, string name, Type type = null)
        {
            Type t = (type == null ? instance.GetType() : type);
            t.GetMethod(name, BindingFlagsAll).Invoke(instance, null);
        }
        #endregion
    }
}
