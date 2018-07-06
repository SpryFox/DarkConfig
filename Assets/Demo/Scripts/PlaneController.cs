using UnityEngine;
using System.Collections.Generic;
using DarkConfig;
using SpryFox.Common;

public class PlaneController : MonoBehaviour {
    public float RotationCommand = 0;
    public bool IsFiring = false;
    public float Throttle = 1;

    public int HitPoints;

    // note that this is a property rather than a field, so that hotloaded
    // changes to the card will affect the MaxHitPoints implicitly
    public int MaxHitPoints {
        get { 
            if(Card == null) return 1;
            if(tag == "Player") {
                // for gameplay reasons, players are more durable
                return Card.HitPoints * 2;
            } else {
                return Card.HitPoints;
            }
        }
    }

    public PlaneCard Card;

    public Transform BulletPrefab;
    public Transform ViewPrefab;

    public GameObject ExplosionPrefab;
    public GameObject ShotFireFXPrefab;

    [HideInInspector]
    public PlaneView View;

    public void Setup(PlaneCard card) {
        var viewTrf = (Transform)Instantiate(ViewPrefab, transform.position, 
            transform.rotation);
        viewTrf.parent = transform;
        View = viewTrf.GetComponent<PlaneView>();
        View.Controller = this;

        UseCard(card);
    }

    public void UseCard(PlaneCard card) {
        // here we take a reference to the card; the card object will get 
        // updated automatically for us if the config hotloads, so we don't
        // have to have special hotloading logic in this class
        Card = card;

        // do this after assigning to Card b/c MaxHitPoints depends on Card
        HitPoints = MaxHitPoints;
        m_lastFiredTimes.Clear();

        // display the view; this has to be after HitPoints is assigned
        // or the view will show the wrong thing
        View.Card = card;
    }

    public void Heal(int points) {
        HitPoints += points;
        if(HitPoints > MaxHitPoints) HitPoints = MaxHitPoints;
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

        transform.position = 
            transform.position + 
            transform.up * Card.Speed * Throttle * Time.fixedDeltaTime;

        var now = Time.fixedTime;
        if(IsFiring) {
            for(int i = 0; i < Card.GunMounts.Count; i++) {
                var mount = Card.GunMounts[i];
                if(m_lastFiredTimes.ContainsKey(mount) && 
                    now - m_lastFiredTimes[mount] < mount.Card.FireInterval) {
                    continue;
                }

                // When we construct a bullet, we copy all its attributes
                // from the configs; this means that hotloading won't affect
                // existing bullets.  That's probably OK; bullets are very
                // ephemeral and it's quick to get more of them, so there isn't
                // much of a time cost to not hotloading them individually.
                // We do very much want hotloading to apply when they're being
                // shot, though, otherwise we'd have to restart the entire game
                // to see changes, which would be slow.
                var gunCard = mount.Card;
                var bulletTrf = (Transform)Instantiate(BulletPrefab,
                    transform.TransformPoint(mount.Location.Pos),
                    transform.rotation);
                var bulletComponent = bulletTrf.GetComponent<Bullet>();
                bulletComponent.Damage = gunCard.BulletDamage;
                bulletComponent.Speed = 
                    Card.Speed * Throttle + gunCard.BulletSpeed;
                bulletTrf.GetComponent<Rigidbody2D>().velocity = 
                    bulletTrf.up * bulletComponent.Speed;
                bulletComponent.Firer = transform;
                bulletTrf.localScale = gunCard.BulletSize.XYZ1();

                var timeToFly = gunCard.BulletRange / bulletComponent.Speed;

                Destroy(bulletTrf.gameObject, timeToFly);

                m_lastFiredTimes[mount] = now;

                // display muzzle flash
                var fx = (GameObject)Instantiate(
                    ShotFireFXPrefab,
                    transform.TransformPoint(mount.Location.Pos),
                    transform.rotation);
                Destroy(fx, 0.5f);
            }
        }
    }

    // this handles the rate-of-fire for the guns
    Dictionary<GunMount, float> m_lastFiredTimes = 
        new Dictionary<GunMount, float>();
}
