[Title( "MP5" )]
public class MP5 : BaseBulletWeapon
{
	[Property] public float Damage { get; set; } = 5.0f;
	[Property] public float PrimaryFireRate { get; set; } = 0.067f;

	bool _isShooting;

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		if ( !CanShoot() )
			return;

		_isShooting = Input.Down( "attack1" );
		if ( _isShooting )
		{
			ShootBullet( player );
		}
	}

	public override bool IsInUse() => _isShooting;

	public void ShootBullet( Player player )
	{
		if ( !CanShoot() )
			return;

		AddShootDelay( PrimaryFireRate );

		var aimConeAmount = GetAimConeAmount();
		var forward = player.EyeTransform.Rotation.Forward.WithAimCone( 0.5f + aimConeAmount * 4f, 0.25f + aimConeAmount * 4f );
		var bulletRadius = 1;

		var tr = Scene.Trace.Ray( player.EyeTransform.ForwardRay with { Forward = forward }, 4096 )
							.IgnoreGameObjectHierarchy( player.GameObject )
							.WithoutTags( "playercontroller" ) // don't hit playercontroller colliders
							.Radius( bulletRadius )
							.UseHitboxes()
							.Run();

		Log.Info( $"{tr.Surface}" );
		Log.Info( $"{tr.Hitbox}" );

		ShootEffects( tr.EndPosition, tr.Hit, tr.Normal, tr.GameObject, tr.Surface );
		TraceAttack( TraceAttackInfo.From( tr, Damage ) );
		TimeSinceShoot = 0;
	}

	// returns 0 for no aim spread, 1 for full aim cone
	float GetAimConeAmount()
	{
		return TimeSinceShoot.Relative.Remap( 0, 0.2f, 1, 0 );
	}
}
