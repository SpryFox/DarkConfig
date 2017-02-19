using UnityEngine;
using System.Collections.Generic;
using DarkConfig;
using SpryFox.Common;

public class PlaneController : MonoBehaviour {
    public float RotationCommand = 0;
    public bool IsFiring = false;
    public float Throttle = 1;

    public int HitPoints;
    public int MaxHitPoints;

    public PlaneCard Card;

    public Transform BulletPrefab;
    public Transform ViewPrefab;

    public GameObject ExplosionPrefab;
    public GameObject ShotFireFXPrefab;

    [HideInInspector]
    public PlaneView View;

    public void Setup(PlaneCard card) {
        var viewTrf = (Transform)Instantiate(ViewPrefab, transform.position, transform.rotation);
        viewTrf.parent = transform;
        View = viewTrf.GetComponent<PlaneView>();
        View.Controller = this;

        UseCard(card);
    }

    public void UseCard(PlaneCard card) {
        if(tag == "Player") {
            MaxHitPoints = card.HitPoints * 2;
        } else {
            MaxHitPoints = card.HitPoints;
        }
        HitPoints = MaxHitPoints;
        m_lastFiredTimes.Clear();

        Card = card;
        View.Card = card;
    }

    public void Heal(int points) {
        HitPoints += points;
        View.Refresh(Card);
    }

    public void TakeDamage(int points) {
        HitPoints -= points;
        View.Refresh(Card);
        if(HitPoints <= 0) {
            gameObject.BroadcastMessage("Killed");
            Destroy(gameObject);
            var explosion = (GameObject)Instantiate(ExplosionPrefab, transform.position, Quaternion.identity);
            Destroy(explosion, 1f);
        }
    }

    void FixedUpdate() {
        var euler = transform.eulerAngles;
        euler.z += RotationCommand * Card.RotationRate * Time.fixedDeltaTime;
        transform.eulerAngles = euler;

        transform.position = transform.position + transform.up * Card.Speed * Throttle * Time.fixedDeltaTime;


        var now = Time.fixedTime;
        if(IsFiring) {
            for(int i = 0; i < Card.GunMounts.Count; i++) {
                var mount = Card.GunMounts[i];
                if(!m_lastFiredTimes.ContainsKey(mount) || 
                    now - m_lastFiredTimes[mount] > mount.Card.FireInterval) {

                    var gunCard = mount.Card;
                    var bulletTrf = (Transform)Instantiate(BulletPrefab, transform.TransformPoint(mount.Location.Pos), transform.rotation);
                    var bulletComponent = bulletTrf.GetComponent<Bullet>();
                    bulletComponent.Damage = gunCard.BulletDamage;
                    bulletComponent.Speed = Card.Speed * Throttle + gunCard.BulletSpeed;
                    bulletTrf.GetComponent<Rigidbody2D>().velocity = bulletTrf.up * bulletComponent.Speed;
                    bulletComponent.Firer = transform;
                    bulletTrf.localScale = gunCard.BulletSize.XYZ1();

                    var timeToFly = gunCard.BulletRange / bulletComponent.Speed;

                    Destroy(bulletTrf.gameObject, timeToFly);

                    m_lastFiredTimes[mount] = now;

                    var fx = (GameObject)Instantiate(ShotFireFXPrefab, transform.TransformPoint(mount.Location.Pos), transform.rotation);
                    Destroy(fx, 0.5f);
                }
            }
        }
    }

    Dictionary<GunMount, float> m_lastFiredTimes = new Dictionary<GunMount, float>();
}
