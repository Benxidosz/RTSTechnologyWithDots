using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour {
    public static GameManager Instance { get; private set; }

    [Header("Point System")] 
    [SerializeField]
    private int point = 0;

    [SerializeField] private TextMeshProUGUI pointText;

    [Header("Soldier Spawning")] 
    [SerializeField]
    private int soldierCost;
    [SerializeField] private GameObject soldierPrefab;
    [SerializeField] private TextMeshProUGUI soldierCostText;
    private int _soldierCountBuy = 0;

    [Header("Tower Building")] [SerializeField]
    private int towerCost = 100;
    [SerializeField] private GameObject towerPrefab;
    [SerializeField] private TextMeshProUGUI towerCostText;
    
    private Camera _mainCamera;

    void Start() {
        if (Instance == null)
            Instance = this;
        else {
            Destroy(this);
        }

        _mainCamera = Camera.main;
        soldierCostText.text = $"{soldierCost} $";
        towerCostText.text = $"{towerCost} $";
        pointText.text = $"{point} $";
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.D)) {
            var cameraTransform = _mainCamera.transform;
            var cameraPos = cameraTransform.position;
            cameraTransform.RotateAround(cameraPos, Vector3.up, -90);
            cameraTransform.position = new Vector3(-cameraPos.z, cameraPos.y, cameraPos.x);
        }

        if (Input.GetKeyDown(KeyCode.A)) {
            var cameraTransform = _mainCamera.transform;
            var cameraPos = cameraTransform.position;
            cameraTransform.RotateAround(cameraPos, Vector3.up, 90);
            cameraTransform.position = new Vector3(cameraPos.z, cameraPos.y, -cameraPos.x);
        }
    }

    public void IncreasePoint(int point) {
        this.point += point;
        pointText.text = $"{this.point} $";
    }

    private void SpawnSoldier() {
        
    }

    private void BuildTower() {
        
    }
    
    public void BuySoldier() {
        if (point < soldierCost)
            return;

        point -= soldierCost;
        SpawnSoldier();
        
        ++_soldierCountBuy;
        if (_soldierCountBuy >= 5) {
            soldierCost += 10;
            soldierCostText.text = $"{soldierCost} $";
            _soldierCountBuy = 0;
        }
    }

    public void BuyTower() {
        if (point < towerCost)
            return;

        point -= towerCost;
        BuildTower();

        towerCost += 100;
        towerCostText.text = $"{towerCost} $";
    }
}