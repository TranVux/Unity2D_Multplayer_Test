using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class NetworkUIManager : MonoBehaviour
{

    [SerializeField] private Button buttonServer;
    [SerializeField] private Button buttonClient;
    [SerializeField] private Button buttonHost;

    private void Awake()
    {
        buttonClient.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartClient();
        });

        buttonServer.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartServer();
        });

        buttonHost.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartHost();
        });
    }
}
