using Sandbox.Rendering;

public class M4a1Weapon : BaseBulletWeapon
{
	[Property] public float TimeBetweenShots { get; set; } = 0.1f;
	[Property] public float Damage { get; set; } = 12.0f;

	protected TimeSince TimeSinceShoot = 0;

	protected override float GetPrimaryFireRate() => TimeBetweenShots;

	public override bool CanPrimaryAttack()
	{
		if ( !HasAmmo() ) return false;
		if ( IsReloading() ) return false;
		if ( TimeUntilNextShotAllowed > 0 ) return false;

		return true;
	}

	public override void PrimaryAttack()
	{
		ShootBullet();
	}

	private void ShootBullet()
	{
		if ( !HasAmmo() || IsReloading() || TimeUntilNextShotAllowed > 0 )
		{
			TryAutoReload();
			return;
		}

		if ( !TakeAmmo( 1 ) )
		{
			AddShootDelay( 0.2f );
			return;
		}

		AddShootDelay( TimeBetweenShots );

		var aimConeAmount = GetAimConeAmount();
		var forward = Owner.EyeTransform.Rotation.Forward.WithAimCone( 0.5f + aimConeAmount * 3f, 0.25f + aimConeAmount * 3f );
		var bulletRadius = 2f;

		var tr = Scene.Trace.Ray( Owner.EyeTransform.ForwardRay with { Forward = forward }, 4096 )
							.IgnoreGameObjectHierarchy( Owner.GameObject )
							.WithoutTags( "playercontroller" ) // don't hit playercontroller colliders
							.Radius( bulletRadius )
							.UseHitboxes()
							.Run();

		ShootEffects( tr.EndPosition, tr.Hit, tr.Normal, tr.GameObject, tr.Surface );
		TraceAttack( TraceAttackInfo.From( tr, Damage ) );
		TimeSinceShoot = 0;

		if ( !Owner.IsValid() )
		{
			return;
		}

		Owner.Controller.EyeAngles += new Angles( Random.Shared.Float( -0.2f, -0.6f ), Random.Shared.Float( -0.2f, 0.2f ), 0 );

		if ( !Owner.Controller.ThirdPerson && Owner.IsLocalPlayer )
		{
			new Sandbox.CameraNoise.Recoil( 2.0f, 0.5f );
		}
	}

	float GetAimConeAmount()
	{
		return TimeSinceShoot.Relative.Remap( 0, 0.2f, 1, 0 );
	}
}
