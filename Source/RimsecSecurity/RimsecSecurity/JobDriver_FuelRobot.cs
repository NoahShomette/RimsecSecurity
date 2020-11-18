﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimsecSecurity
{
    class JobDriver_FuelRobot : JobDriver
    {
        private const TargetIndex RefuelableInd = TargetIndex.A;
        private const TargetIndex FuelInd = TargetIndex.B;
        private const int RefuelingDuration = 240;
        protected Pawn Refuelable => this.job.GetTarget(TargetIndex.A).Pawn;
        protected Thing Fuel => this.job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => 
            this.pawn.Reserve(this.Refuelable, this.job, 1, -1, null, errorOnFailed) 
            && this.pawn.Reserve(this.Fuel, this.job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            base.AddEndCondition(delegate
            {
                if (Refuelable.needs.rest.CurLevel < 0.9)
                {
                    return JobCondition.Ongoing;
                }
                return JobCondition.Succeeded;
            });
            //base.AddFailCondition(() => !this.job.playerForced);
            yield return Toils_General.DoAtomic(delegate
            {
                this.job.count = Convert.ToInt32((Refuelable.needs.rest.MaxLevel - Refuelable.needs.rest.CurLevel) * 100);
                if (this.job.count > 75) job.count = 75;
            }).FailOn(() => job.count == 0);
            Toil reserveFuel = Toils_Reserve.Reserve(TargetIndex.B, 1, -1, null);
            yield return reserveFuel;
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, true, false).FailOnDestroyedNullOrForbidden(TargetIndex.B);
            yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveFuel, TargetIndex.B, TargetIndex.None, true, null);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(240, TargetIndex.None).FailOnDestroyedNullOrForbidden(TargetIndex.B).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch).WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                //Log.Message($"current rest: {Refuelable.needs.rest.CurLevel} stackcount: {Fuel.stackCount} calced {(Fuel.stackCount / 100f)}");
                Refuelable.needs.rest.CurLevel += (Fuel.stackCount / 100f);
                Fuel.Destroy();
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return toil;
            yield break;
        }

        public Toil FinalizeRefueling(TargetIndex refuelableInd, TargetIndex fuelInd)
        {
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                Job curJob = toil.actor.CurJob;
                var thing = curJob.GetTarget(refuelableInd).Pawn;
                if (toil.actor.CurJob.placedThings.NullOrEmpty<ThingCountClass>())
                {
                    return;
                }
                var placed = curJob.placedThings;
                //Log.Message($"Count placed things: {placed.Count} first placed count: {placed.FirstOrDefault().Count} {placed.FirstOrDefault().thing.Label} {placed.FirstOrDefault().thing.stackCount}");
                thing.needs.rest.CurLevel += (placed.FirstOrDefault().thing.stackCount / 100);
                placed.FirstOrDefault().thing.Destroy();
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        public void Refuel(List<Thing> fuelThings)
        {
            int num = Convert.ToInt32((Refuelable.needs.rest.MaxLevel - Refuelable.needs.rest.CurLevel) * 100);
            while (num > 0 && fuelThings.Count > 0)
            {
                Thing thing = fuelThings.Pop<Thing>();
                int num2 = Mathf.Min(num, thing.stackCount);
                Refuelable.needs.rest.CurLevel += (num2 / 100);
                thing.SplitOff(num2).Destroy(DestroyMode.Vanish);
                num -= num2;
            }
        }


    }
}
