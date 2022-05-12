using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Components;
using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = Unity.Mathematics.Random;

public class GameManager : MonoBehaviour {
    public static GameManager Instance { get; private set; }

    [Header("Point System")] 
    [SerializeField]
    private int point = 0;

    [SerializeField] private TextMeshProUGUI pointText;

    [Header("Spawner")] 
    [SerializeField] private Transform coreTransform;

    private EntityManager _entityManager;
    private BlobAssetStore _blobAssetStore;

    [Header("Soldier Spawning")] 
    [SerializeField]
    private int soldierCost;
    [SerializeField] private GameObject soldierPrefab;
    [SerializeField] private TextMeshProUGUI soldierCostText;
    [SerializeField] private float minSoldierRadius;
    [SerializeField] private float maxSoldierRadius;
    private int _soldierCountBuy = 0;
    private Entity _soldierEntity;

    [Header("Tower Building")] [SerializeField]
    private int towerCost = 100;
    [SerializeField] private GameObject towerPrefab;
    [SerializeField] private TextMeshProUGUI towerCostText;
    
    private Camera _mainCamera;
    private float3 _corePos;
    private Random rand;

    private void Awake() {
        if (Instance == null)
            Instance = this;
        else {
            Destroy(this);
        }

        _corePos = coreTransform.position;
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _blobAssetStore = new BlobAssetStore();
        var settings = GameObjectConversionSettings.FromWorld(World.DefaultGameObjectInjectionWorld, _blobAssetStore);
        _soldierEntity = GameObjectConversionUtility.ConvertGameObjectHierarchy(soldierPrefab, settings);
        rand = new Unity.Mathematics.Random((uint)Stopwatch.GetTimestamp());
        var radius = rand.NextFloat(minSoldierRadius, maxSoldierRadius);
        var alfa = rand.NextFloat(0.0f, 2 * math.PI);
        var dest = _corePos + new float3(math.cos(alfa), 0.0f, math.sin(alfa)) * radius;
        var entity = _entityManager.Instantiate(_soldierEntity);
        _entityManager.SetComponentData(entity, new Translation {
            Value = new float3(0f, -5f, 0f)
        });
        _entityManager.RemoveComponent<Shooting>(entity);
    }

    void Start() {
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
        var alfa = rand.NextFloat(0.0f, 2 * math.PI);
        var radius = rand.NextFloat(minSoldierRadius, maxSoldierRadius);
        var dest = _corePos + new float3(math.cos(alfa), 0.0f, math.sin(alfa)) * radius;

        var entity = _entityManager.Instantiate(_soldierEntity);
        _entityManager.SetComponentData(entity, new Translation {
            Value = _corePos
        });
        _entityManager.AddComponent<SoldierMovement>(entity);
        _entityManager.SetComponentData(entity, new SoldierMovement {
            destination = dest,
            speed = 5f
        });
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

    private void OnDestroy() {
        _blobAssetStore.Dispose();
    }
}