using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StatusOverloadValidation
{
    public static void Run()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            G.Enemies.Clear();
            G.DataBase.ExplosionPrefab = null;

            Enemy fire = CreateEnemy(Vector3.zero);
            fire.AddDots("огонь", 1, 2, 3);
            fire.TickStatuses();
            Check(fire.FireOverloadProgress, 2, "First fire tick");
            fire.TickStatuses();
            Check(fire.FireOverloadProgress, 4, "Second fire tick");
            fire.TickStatuses();
            Check(fire.Health, 979, "Final tick natural explosion");
            Check(fire.FireOverloadProgress, 0, "Natural progress reset");

            Enemy forced = CreateEnemy(new Vector3(20, 0));
            forced.AddDots("огонь", 1, 2, 3);
            forced.AddDots("кровь", 1, 2.5f, 2);
            CombatProfile profile = new CombatProfile();
            profile.Apply(new AmmoProperties { Overload = true });
            forced.ReceiveHit(profile, Vector3.zero);
            Check(forced.Health, 975, "Early explosion full damage");
            Check(forced.Count("огонь"), 0, "Forced fire consumption");
            Check(forced.Count("кровь"), 1, "Unrelated bleed preserved");

            Enemy poison = CreateEnemy(new Vector3(40, 0));
            Enemy neighbor = CreateEnemy(new Vector3(41, 0));
            poison.AddDots("яд", 4, 1.5f, 4);
            Enemy.TickAllStatuses(G.Enemies);
            Check(poison.Health, 984, "Natural poison damage");
            Check(poison.Count("яд"), 4, "Natural poison charges preserved");
            Check(neighbor.Health, 990, "Spread must not tick immediately");
            Check(neighbor.Count("яд"), 1, "Poison spread charge");
            Check(neighbor.PoisonOverloadProgress, 0, "Spread progress deferred");

            Debug.Log("STATUS_OVERLOAD_VALIDATION_PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static Enemy CreateEnemy(Vector3 position)
    {
        GameObject root = new GameObject("Status validation enemy", typeof(SpriteRenderer));
        root.transform.position = position;
        Enemy enemy = root.AddComponent<Enemy>();
        MethodInfo awake = typeof(Enemy).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
        awake.Invoke(enemy, null);
        enemy.Configure(1000);
        G.Enemies.Add(enemy);
        return enemy;
    }

    private static void Check(float actual, float expected, string message)
    {
        if (!Mathf.Approximately(actual, expected))
        {
            throw new Exception(message + ": expected " + expected + ", got " + actual);
        }
    }
}
