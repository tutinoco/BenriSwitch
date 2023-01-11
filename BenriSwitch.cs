using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace tutinoco
{
    public enum BenriSwitchType
    {
        Normal,
        Trigger,
        Area,
    }

    public enum BenriSwitchLinkType
    {
        Manual,
        Sync,
        Radio,
    }

    public class BenriSwitch : UdonSharpBehaviour
    {
        [Header("スイッチの種類を設置")]
        public BenriSwitchType type;

        [Header("グローバルスイッチに設定（チェックしないとローカルになります）")]
        public bool isGlobal;

        [Header("スイッチの初期状態を設定（チェックすると最初からONになります）")]
        public bool isON;

        [Header("スイッチ切り替え時に鳴らす音を設定")]
        public AudioSource audioSource;
        public AudioClip onSound;
        public AudioClip offSound;

        [Header("押したスイッチが戻る時間を設定（0なら無効、2.0なら2秒後に戻ります）")]
        public float backTimer;
        public bool isDisabledDuringOn;
        private int backTimerCount;
        private bool defaultState;

        [Header("Triggerタイプのスイッチで指を離した時にOFFにするように設定")]
        public bool isOffWhenReleased;

        [Header("他スイッチとリンクして動作させる他のスイッチを登録")]
        public BenriSwitchLinkType linkType;
        public BenriSwitch[] links;

        [Header("スイッチON/OFF時に有効にするオブジェクトを登録")]
        public GameObject[] activeObjects;
        public GameObject[] disableObjects;

        [Header("スイッチON/OFFした瞬間に実行したいイベントを設定")]
        public UdonSharpBehaviour onEventTarget;
        public string onEventName;
        public UdonSharpBehaviour offEventTarget;
        public string offEventName;

        [Header("ダブルクリックした時に表示するオブジェクトを登録")]
        public float doubleSpeed;
        public bool isOnTogether;
        public GameObject[] doubleObjects;
        private readonly bool isDouble;

        [Header("スイッチのコライダーを他に変更したい場合に設定（またAreaスイッチの範囲を設定）")]
        public Collider collider;

        [Header("スイッチを操作できる人を登録（空なら誰でも操作できます）")]
        public string[] memberships;

        private bool isInArea;

        void Start()
        {
            defaultState = isON;
            if ( !collider ) collider = GetComponent<Collider>();
            UpdateObjects();
        }

        private void Update()
        {
            if (backTimerCount >= 0) backTimerCount--;
            if (backTimerCount == 0) Switch();
            if (isDisabledDuringOn) collider.enabled = !isON;

            if ( type == BenriSwitchType.Area ) {
                Vector3 playerPos = Networking.LocalPlayer.GetPosition();
                bool b = 0.1f > Vector3.Distance(collider.ClosestPoint(playerPos), playerPos);
                if ( isInArea != b ) {
                    if ( !isON == b ) Switch();
                    if ( isON == !b ) Switch();
                }
                isInArea = b;
            }                
        }

        private void UpdateObjects()
        {
            for (int i=0; i < activeObjects.Length; i++) {
                GameObject obj = activeObjects[i];
                if( obj != null ) obj.SetActive(isON);
            }
            for (int i=0; i < disableObjects.Length; i++) {
                GameObject obj = disableObjects[i];
                if( obj != null ) obj.SetActive(!isON);
            }
        }

        public override void Interact()
        {
            if ( type == BenriSwitchType.Trigger ) return;
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
            backTimerCount = defaultState==isON ? -1 : (int)(backTimer * 60);

            Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
            NetworkEventTarget nwTarget = isGlobal ? NetworkEventTarget.All : NetworkEventTarget.Owner;
            SendCustomNetworkEvent(nwTarget, isON ? nameof(SyncON) : nameof(SyncOFF));

            UpdateLinks();
        }

        public void UpdateLinks()
        {
            foreach (BenriSwitch obj in links) {
                if ( obj == this ) continue;
                if ( linkType==BenriSwitchLinkType.Sync && isON!=obj.isON ) {
                    string[] es = isON ? new string[]{"SyncON_event", "SyncON_silent"} : new string[]{"SyncOFF_event", "SyncOFF_silent"};
                    foreach( string e in es ) {
                        if( obj.isGlobal ) obj.SendCustomNetworkEvent(NetworkEventTarget.All, e);
                        else obj.SendCustomEvent(e);
                    }
                }
                if ( linkType==BenriSwitchLinkType.Radio && obj.isON ) {
                    foreach( string e in new string[] {"SyncOFF_event", "SyncOFF_silent"} ) {
                        if( obj.isGlobal ) obj.SendCustomNetworkEvent(NetworkEventTarget.All, e);
                        else obj.SendCustomEvent(e);
                    }
                }
            }
        }

        public override void OnPickupUseUp()
        {
            if (type!=BenriSwitchType.Trigger || !isOffWhenReleased) return;
            if (audioSource && offSound) audioSource.PlayOneShot(offSound);
            Switch();
        }

        public override void OnPickupUseDown()
        {
            if (type!=BenriSwitchType.Trigger) return;
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
            SyncON_event();
            if (audioSource && onSound) audioSource.PlayOneShot(onSound);
        }

        public void SyncON_silent()
        {
            isON = true;
            UpdateObjects();
        }

        public void SyncON_event()
        {
            if (onEventTarget) onEventTarget.SendCustomEvent(onEventName);
        }

        public void SyncOFF()
        {
            SyncOFF_event();
            SyncOFF_silent();
            if (audioSource && offSound) audioSource.PlayOneShot(offSound);
        }

        public void SyncOFF_silent()
        {
            isON = false;
            UpdateObjects();
        }

        public void SyncOFF_event()
        {
            if (offEventTarget) offEventTarget.SendCustomEvent(offEventName);
        }
    }
}
