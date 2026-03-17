using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Windy Airdrop", "я и я", "1.0.0")]

    public class WindyAirdrop : CovalencePlugin
    {
        #region ’уки
        private bool initComplete = false;

        private void OnServerInitialized()
        {
            initComplete = true;
        }

        private void OnEntitySpawned(SupplyDrop supplyDrop)
        {
            if (!initComplete) return;
            var windModifier = supplyDrop.gameObject.AddComponent<SupplyDropModifier>();
        }
        #endregion

        #region јир

        private class SupplyDropModifier : MonoBehaviour
        {
            SupplyDrop supplyDrop;
            BaseEntity chute;

            Vector3 windDir;
            Vector3 newDir;
            float windSpeed;
            int counter;
            bool dropinit = false;

            private void Awake()
            {
                supplyDrop = GetComponent<SupplyDrop>();
                if (supplyDrop == null) { OnDestroy(); return; }
                chute = supplyDrop.parachute;
                if (chute == null) { OnDestroy(); return; }

                windDir = GetDirection();
                windSpeed = 10f;
                counter = 0;
                dropinit = true;
            }

            private Vector3 GetDirection()
            {
                var direction = Random.insideUnitSphere * 0f;
                if (direction.y > -windSpeed) direction.y = -windSpeed;
                return direction;
            }

            private void FixedUpdate()
            {
                if (!dropinit) return;
                if (chute == null || supplyDrop == null) { OnDestroy(); return; }
                newDir = Vector3.RotateTowards(transform.forward, windDir, 0.5f * Time.deltaTime, 0.0F);
                newDir.y = 0f;
                supplyDrop.transform.position = Vector3.MoveTowards(transform.position, transform.position + windDir, (windSpeed) * Time.deltaTime);
                supplyDrop.transform.rotation = Quaternion.LookRotation(newDir);
                if (counter == 0) { windDir = GetDirection(); counter = 0; }
                counter++;
            }

            private void OnDestroy()
            {
                GameObject.Destroy(this);
            }
        }
        #endregion
    }
}