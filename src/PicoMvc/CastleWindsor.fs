module Strangelights.PicoMvc.CastleWindsor
open Strangelights.PicoMvc
open Castle.Windsor

let getCastleWindsorParameterAction (container: IWindsorContainer) =
    let canTreat _ _ (x: System.Type) =
        container.Kernel.HasComponent x
    let deserializeAsJson _ _ (x: System.Type) =
        container.Resolve x
    { CanTreatParameter = canTreat
      ParameterAction = deserializeAsJson }
