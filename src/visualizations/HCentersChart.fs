[<RequireQualifiedAccess>]
module HCentersChart

open System
open Elmish
open Feliz
open Feliz.ElmishComponents

open Types
open Data.HCenters
open Highcharts

type Scope =
    | Totals
    | Region of string

type State = {
    scope : Scope
    hcData : HcStats []
    error: string option
  } with
    static member initial =
        {
            scope = Totals
            hcData = [||]
            error = None
        }

type Msg =
    | ConsumeHcData of Result<HcStats [], string>
    | ConsumeServerError of exn
    | SwitchScope of Scope

let getAllScopes state = seq {
    yield Totals, "Vse"
    for region in Utils.Dictionaries.regions do
        if not (Set.contains region.Key Utils.Dictionaries.excludedRegions)
        then yield Region region.Key, region.Value.Name
}

let init () : State * Cmd<Msg> =
    let cmd = Cmd.OfAsync.either Data.HCenters.getOrFetch () ConsumeHcData ConsumeServerError
    State.initial, (cmd)

let update (msg: Msg) (state: State) : State * Cmd<Msg> =
    match msg with
    | ConsumeHcData (Ok data) ->
        { state with hcData = data }, Cmd.none
    | ConsumeHcData (Error err) ->
        { state with error = Some err }, Cmd.none
    | ConsumeServerError ex ->
        { state with error = Some ex.Message }, Cmd.none
    | SwitchScope scope ->
        { state with scope = scope }, Cmd.none

let renderChartOptions (state : State) =
    let className = "hcenters-chart"
    let scaleType = ScaleType.Linear
    let startDate = DateTime(2020,3,18)
    let mutable startTime = startDate |> jsTime

    let getRegionStats region mp = 
        mp |> Map.find region |> Map.find "ljubljana" 

    let hcData = 
        match state.scope with
        | Totals        -> state.hcData |> Seq.map (fun dp -> (dp.Date, dp.all)) |> Seq.toArray
        | Region region -> state.hcData |> Seq.map (fun dp -> (dp.Date, getRegionStats region dp.municipalities)) |> Seq.toArray

    let allSeries = [
        yield pojo
            {|
                name = I18N.t "charts.hCenters.emergencyExamination"
                ``type`` = "line"
                color = "#70a471"
                dashStyle = Dot |> DashStyle.toString
                data = hcData |> Seq.map (fun (date,dp) -> (date |> jsTime12h, dp.examinations.medicalEmergency)) |> Seq.toArray
            |}

        yield pojo
            {|
                name = I18N.t "charts.hCenters.suspectedCovidExamination"
                ``type`` = "line"
                color = "#a05195"
                dashStyle = Dot |> DashStyle.toString
                data = hcData |> Seq.map (fun (date,dp) -> (date |> jsTime12h, dp.examinations.suspectedCovid)) |> Seq.toArray
            |}
        yield pojo
            {|
                name = I18N.t "charts.hCenters.suspectedCovidPhone"
                ``type`` = "line"
                color = "#d45087"
                dashStyle = Dot |> DashStyle.toString
                data = hcData |> Seq.map (fun (date,dp) -> (date |> jsTime12h, dp.phoneTriage.suspectedCovid)) |> Seq.toArray
            |}
        yield pojo
            {|
                name = I18N.t "charts.hCenters.sentToSelfIsolation"
                ``type`` = "line"
                color = "#665191"
                data = hcData |> Seq.map (fun (date,dp) -> (date |> jsTime12h, dp.sentTo.selfIsolation)) |> Seq.toArray
            |}
        yield pojo
            {|
                name = I18N.t "charts.hCenters.testsPerformed"
                ``type`` = "line"
                color = "#19aebd"
                data = hcData |> Seq.map (fun (date,dp) -> (date |> jsTime12h, dp.tests.performed)) |> Seq.toArray
            |}
        yield pojo
            {|
                name = I18N.t "charts.hCenters.testsPositive"
                ``type`` = "line"
                color = "#d5c768"
                data = hcData |> Seq.map (fun (date,dp) -> (date |> jsTime12h, dp.tests.positive)) |> Seq.toArray
            |}
        yield addContainmentMeasuresFlags startTime None |> pojo

    ]

    let baseOptions = Highcharts.basicChartOptions scaleType className
    {| baseOptions with
        series = List.toArray allSeries

        // need to hide negative label for addContainmentMeasuresFlags
        yAxis = baseOptions.yAxis |> Array.map (fun ax -> {| ax with showFirstLabel = false |})

        legend = pojo {| enabled = true ; layout = "horizontal" |}
    |}

let renderChartContainer (state : State) =
    Html.div [
        prop.style [ style.height 480 ]
        prop.className "highcharts-wrapper"
        prop.children [
            renderChartOptions state
            |> Highcharts.chart
        ]
    ]

let renderScopeSelector state scope (name:string) onClick =
    Html.div [
        prop.onClick onClick
        prop.className [ true, "btn btn-sm metric-selector"; state.scope = scope, "metric-selector--selected" ]
        prop.text name
    ]

let renderScopeSelectors state dispatch =
    Html.div [
        prop.className "metrics-selectors"
        prop.children (
            getAllScopes state
            |> Seq.map (fun (scope,name) ->
                renderScopeSelector state scope name (fun _ -> SwitchScope scope |> dispatch)
            ) ) ]

let render (state : State) dispatch =
    match state.hcData, state.error with
    | [||], None -> Html.div [ Utils.renderLoading ]
    | _, Some err -> Html.div [ Utils.renderErrorLoading err ]
    | _, None ->
        Html.div [
            renderChartContainer state
            renderScopeSelectors state dispatch

            Html.div [
                prop.className "disclaimer"
                prop.children [
                    Html.text (I18N.t "charts.common.noteFaq")
                    Html.a
                        [ prop.className "faq-link"
                          prop.target "_blank"
                          prop.href "/FAQ/#hcenters-chart"
                          prop.text (I18N.t "charts.common.linkFaq") ]
                ]
            ]
        ]


let hCentersChart () =
    React.elmishComponent("HCentersChart", init (), update, render)
