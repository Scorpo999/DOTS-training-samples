﻿using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Random = Unity.Mathematics.Random;

// Systems can schedule work to run on worker threads.
// However, creating and removing Entities can only be done on the main thread to prevent race conditions.
// The system uses an EntityCommandBuffer to defer tasks that can't be done inside the Job.

// ReSharper disable once InconsistentNaming
[UpdateInGroup(typeof(SimulationSystemGroup))]
public class LineSpawnerSystem_FromEntity : SystemBase
{
    // BeginInitializationEntityCommandBufferSystem is used to create a command buffer which will then be played back
    // when that barrier system executes.
    // Though the instantiation command is recorded in the SpawnJob, it's not actually processed (or "played back")
    // until the corresponding EntityCommandBufferSystem is updated. To ensure that the transform system has a chance
    // to run on the newly-spawned entities before they're rendered for the first time, the SpawnerSystem_FromEntity
    // will use the BeginSimulationEntityCommandBufferSystem to play back its commands. This introduces a one-frame lag
    // between recording the commands and instantiating the entities, but in practice this is usually not noticeable.
    BeginInitializationEntityCommandBufferSystem m_EntityCommandBufferSystem;

    protected override void OnCreate()
    {
        // Cache the BeginInitializationEntityCommandBufferSystem in a field, so we don't have to create it every frame
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        // Schedule the Entities.ForEach lambda job that will add Instantiate commands to the EntityCommandBuffer.
        // Since this job only runs on the first frame, we want to ensure Burst compiles it before running to get the best performance (3rd parameter of WithBurst)
        // The actual job will be cached once it is compiled (it will only get Burst compiled once).
        Entities
            .WithName("LineSpawnerSystem_FromEntity")
            .WithBurst(FloatMode.Default, FloatPrecision.Standard, true)
            .WithStructuralChanges()
            .ForEach((Entity spawnerEntity, in LineSpawner_FromEntity lineSpawnerFromEntity, in Translation translation) =>
        {
            var tileSpawner = GetSingleton<TileSpawner>();
            var worldSizeX = tileSpawner.XSize * tileSpawner.Scale;
            var worldSizeY = tileSpawner.YSize * tileSpawner.Scale;
            var botDisplaySettings = GetSingleton<BotDisplaySettings>();
            
            // var targetPosition = new TargetPosition() { Value = new float3(0, 0, 0) }; // TEMP MOVEMENT

            var random = new Random(1);
            for (var x = 0; x < lineSpawnerFromEntity.Count; x++)
            {
                var instance = EntityManager.Instantiate(lineSpawnerFromEntity.LinePrefab);

                // var line = new Line();
                var position = new Translation { Value = new float3(random.NextFloat(0, worldSizeX), 0, random.NextFloat(0, worldSizeY)) };

                for (var a = 0; a < lineSpawnerFromEntity.CountOfFullPassBots; a++)
                {
                    var bot = EntityManager.Instantiate(lineSpawnerFromEntity.BotPrefab);
                    var botPosition = new Translation { Value = new float3(random.NextFloat(0, worldSizeX), 0, random.NextFloat(0, worldSizeY)) };
                    var botColourPasserFull = new Color() { Value = botDisplaySettings.BotRolePasserFull };
                    EntityManager.AddComponentData(bot, botPosition);
                    EntityManager.AddComponentData(bot, new BotRolePasserFull());
                    EntityManager.AddComponentData(bot, botColourPasserFull);
                    
                    // EntityManager.AddComponentData(bot, targetPosition); //TEMP
                }

                for (var a = 0; a < lineSpawnerFromEntity.CountOfEmptyPassBots; a++)
                {
                    var bot = EntityManager.Instantiate(lineSpawnerFromEntity.BotPrefab);
                    var botPosition = new Translation { Value = new float3(random.NextFloat(0, worldSizeX), 0, random.NextFloat(0, worldSizeY)) };
                    var botColourPasserEmpty = new Color() { Value = botDisplaySettings.BotRolePasserEmpty };
                    EntityManager.AddComponentData(bot, botPosition);
                    EntityManager.AddComponentData(bot, new BotRolePasserEmpty());
                    EntityManager.AddComponentData(bot, botColourPasserEmpty);

                    // EntityManager.AddComponentData(bot, targetPosition); //TEMP
                }

                var botFiller = EntityManager.Instantiate(lineSpawnerFromEntity.BotPrefab);
                var botFillerPosition = new Translation { Value = new float3(random.NextFloat(0, worldSizeX), 0, random.NextFloat(0, worldSizeY)) };
                var botColourFiller = new Color() { Value = botDisplaySettings.BotRoleFiller };
                EntityManager.AddComponentData(botFiller, botFillerPosition);
                EntityManager.AddComponentData(botFiller, new BotRoleFiller());
                EntityManager.AddComponentData(botFiller, botColourFiller);

                var botTosser = EntityManager.Instantiate(lineSpawnerFromEntity.BotPrefab);
                var botTosserPosition = new Translation { Value = new float3(random.NextFloat(0, worldSizeX), 0, random.NextFloat(0, worldSizeY)) };
                var botColourTosser = new Color() { Value = botDisplaySettings.BotRoleTosser };
                EntityManager.AddComponentData(botTosser, botTosserPosition);
                EntityManager.AddComponentData(botTosser, new BotRoleTosser());
                EntityManager.AddComponentData(botFiller, botColourTosser);

                var botFinder = EntityManager.Instantiate(lineSpawnerFromEntity.BotPrefab);
                var botFinderPosition = new Translation { Value = new float3(random.NextFloat(0, worldSizeX), 0, random.NextFloat(0, worldSizeY)) };
                var botColourFinder = new Color() { Value = botDisplaySettings.BotRoleFinder };
                EntityManager.AddComponentData(botFinder, botFinderPosition);
                EntityManager.AddComponentData(botFinder, new BotRoleFinder());
                EntityManager.AddComponentData(botFiller, botColourFinder);

                EntityManager.AddComponentData(instance, position);
            }

            EntityManager.DestroyEntity(spawnerEntity);
        }).Run();

        // SpawnJob runs in parallel with no sync point until the barrier system executes.
        // When the barrier system executes we want to complete the SpawnJob and then play back the commands (Creating the entities and placing them).
        // We need to tell the barrier system which job it needs to complete before it can play back the commands.
        m_EntityCommandBufferSystem.AddJobHandleForProducer(Dependency);
    }
}