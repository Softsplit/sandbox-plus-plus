public sealed class WorldModel : WeaponModel
{
	public void OnAttack()
	{
		Renderer?.Set( "b_attack", true );

		DoMuzzleEffect();
		DoEjectBrass();
	}
}
