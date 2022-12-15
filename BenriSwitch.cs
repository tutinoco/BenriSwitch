using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace tutinoco
{
    public class BenriSwitch : UdonSharpBehaviour
    {
        [Header("グローバルスイッチに設定（チェックしないとローカルになります）")]
        public bool isGlobal;

        [Header("スイッチの初期状態を設定（チェックすると最初からONになります）")]
        public bool isON;

        [Header("スイッチ切り替え時に鳴らす音を設定")]
        public AudioSource audioSource;
        public AudioClip   onSound;
        public AudioClip   offSound;

        [Header("押したスイッチが戻る時間を設定（0なら無効、2.0なら2秒後に戻ります）")]
        public float backTime;
        public bool isDisabledDuringOn;
        private int backTimeCount;
        private bool defaultState;

        [Header("握って押すタイプの引き金式スイッチに設定")]
        public bool isTriggerSwitch;
        public bool isOffWhenReleased;

        [Header("ラジオボタンとして利用する場合に他のスイッチを登録")]
        public BenriSwitch[] radioGroups;

        [Header("スイッチON/OFF時に有効にするオブジェクトを登録")]
        public GameObject[] activeObjects;
        public GameObject[] disableObjects;

        [Header("スイッチON/OFFした瞬間に実行したいイベントを設定")]
        public bool isOwnerOnly;
        public UdonSharpBehaviour onEventTarget;
        public string onEventName;
        public UdonSharpBehaviour offEventTarget;
        public string offEventName;

        [Header("ダブルクリックした時に表示するオブジェクトを登録")]
        public float doubleSpeed;
        public bool isOnTogether;
        public GameObject[] doubleObjects;
        private readonly bool isDouble;

        [Header("スイッチを操作できる人を登録（空なら誰でも操作できます）")]
        public string[] memberships;

        void Start()
        {
            defaultState = isON;
            UpdateObjects();
            if (radioGroups.Length > 0) isDisabledDuringOn = true;
        }

        private void Update()
        {
            if (backTimeCount >= 0) backTimeCount--;
            if (backTimeCount == 0) Switch();
            if (isDisabledDuringOn) GetComponent<Collider>().enabled = !isON;
        }

        private void UpdateObjects()
        {
            for (int i=0; i < activeObjects.Length; i++) {
                GameObject obj = activeObjects[i];
                obj.SetActive(isON);
            }
            for (int i=0; i < disableObjects.Length; i++) {
                GameObject obj = disableObjects[i];
                obj.SetActive(!isON);
            }
        }

        public override void Interact()
        {
            if (isTriggerSwitch) return;
            Switch();
        }

        public void Switch()
        {
            if (memberships.Length > 0) {
                bool flg = false;
                string user_name = Networking.LocalPlayer.displayName;
                for (int i = 0; i < memberships.Length; i++) {
                    if (memberships[i] == user_name) {
                        flg = true;
                        break;
                    }
                }

                if (!flg) return;
            }

            isON = !isON;
            backTimeCount = defaultState==isON ? -1 : (int)(backTime * 60);

            Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
            NetworkEventTarget nwTarget = isGlobal ? NetworkEventTarget.All : NetworkEventTarget.Owner;
            SendCustomNetworkEvent(nwTarget, isON ? nameof(SyncON) : nameof(SyncOFF));

            if (isON && onEventTarget) {
                if (isGlobal) onEventTarget.SendCustomNetworkEvent((isOwnerOnly?NetworkEventTarget.Owner:NetworkEventTarget.All), onEventName);
                else onEventTarget.SendCustomEvent(onEventName);
            }

            if (!isON && offEventTarget) {
                if (isGlobal) offEventTarget.SendCustomNetworkEvent((isOwnerOnly?NetworkEventTarget.Owner:NetworkEventTarget.All), offEventName);
                else offEventTarget.SendCustomEvent(offEventName);
            }

            foreach (BenriSwitch obj in radioGroups) {
                if (obj == this ) continue;
                obj.SendCustomNetworkEvent(nwTarget, nameof(SyncOFF_silent));
            }
        }

        public override void OnPickupUseUp()
        {
            if (!isTriggerSwitch || !isOffWhenReleased) return;
            if (audioSource && offSound) audioSource.PlayOneShot(offSound);
            Switch();
        }

        public override void OnPickupUseDown()
        {
            if (!isTriggerSwitch) return;
            if (audioSource && onSound) audioSource.PlayOneShot(onSound);
            Switch();
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (!isGlobal || !Networking.IsMaster) return;
            string e = isON ? nameof(SyncON_silent) : nameof(SyncOFF_silent);
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, e);
        }

        public void SyncON()
        {
            SyncON_silent();
            if (audioSource && onSound) audioSource.PlayOneShot(onSound);
        }

        public void SyncON_silent()
        {
            isON = true;
            UpdateObjects();
       }

        public void SyncOFF()
        {
            SyncOFF_silent();
            if (audioSource && offSound) audioSource.PlayOneShot(offSound);
        }

        public void SyncOFF_silent()
        {
            isON = false;
            UpdateObjects();
        }
    }
}
