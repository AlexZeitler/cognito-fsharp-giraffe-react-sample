module SampleAPI.App

open System
open System.IO
open FSharpx.Collections
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Microsoft.IdentityModel.Tokens

// ---------------------------------
// Models
// ---------------------------------

type Message = { Text: string }

// ---------------------------------
// Views
// ---------------------------------

module Config =
  let region = "eu-central-1" // your AWS region
  let userPoolId = "eu-central-1_rypunXn4M" // CognitoStack.userpoolId
  let clientId = "3ot29hu8k3cbikij2jat97sqou" // CognitoStack.reactClientId
  let scopeName = "giraffe-server/api" // CognitoStack.scopeName
  let clientUri = "http://localhost:3000"

module Views =
  open Giraffe.ViewEngine

  let layout (content: XmlNode list) =
    html [] [
      head [] [
        title [] [ encodedText "SampleAPI" ]
        link [ _rel "stylesheet"
               _type "text/css"
               _href "/main.css" ]
      ]
      body [] content
    ]

  let partial () = h1 [] [ encodedText "SampleAPI" ]

  let index (model: Message) =
    [ partial ()
      p [] [ encodedText model.Text ] ]
    |> layout

// ---------------------------------
// Web app
// ---------------------------------

let indexHandler (name: string) =
  let greetings = sprintf "Hello %s, from Giraffe!" name
  let model = { Text = greetings }
  let view = Views.index model
  htmlView view

[<Authorize("ApiScope")>]
let greet: HttpHandler =
  fun (next: HttpFunc) (ctx: HttpContext) ->
    let name = "World"
    text ("Hello " + name) next ctx

let authenticate: HttpFunc -> HttpContext -> HttpFuncResult =
  requiresAuthentication (challenge JwtBearerDefaults.AuthenticationScheme)

let unauthorized = setStatusCode 403

let authorize =
  authorizeByPolicyName ("ApiScope") unauthorized

let webApp =
  choose [ GET
           >=> choose [ route "/" >=> indexHandler "world"
                        route "/greet" >=> authorize >=> greet
                        routef "/hello/%s" indexHandler ]
           setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex: Exception) (logger: ILogger) =
  logger.LogError(ex, "An unhandled exception has occurred while executing the request.")

  clearResponse
  >=> setStatusCode 500
  >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder: CorsPolicyBuilder) =
  builder
    .WithOrigins(Config.clientUri)
    .AllowAnyMethod()
    .AllowAnyHeader()
  |> ignore

let configureApp (app: IApplicationBuilder) =
  let env =
    app.ApplicationServices.GetService<IWebHostEnvironment>()

  (match env.IsDevelopment() with
   | true -> app.UseDeveloperExceptionPage()
   | false ->
       app
         .UseGiraffeErrorHandler(errorHandler)
         .UseHttpsRedirection())
    .UseCors(configureCors)
    .UseStaticFiles()
    .UseAuthentication()
    .UseAuthorization()
    .UseRouting()
    .UseGiraffe(webApp)

let authenticationOptions (o: AuthenticationOptions) =
  o.DefaultAuthenticateScheme <- JwtBearerDefaults.AuthenticationScheme
  o.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme

open ScopeAuthorizationRequirement

let authzOptions (o: AuthorizationPolicyBuilder) =
  o.RequireAuthenticatedUser() |> ignore

  let array =
    [| Config.scopeName
       "openid"
       "profile"
       "email" |]

  o.RequireScopes(NonEmptyList.ofArray (array))
  |> ignore

let authorizationOptions (o: AuthorizationOptions) = o.AddPolicy("ApiScope", authzOptions)

let jwtBearerOptions (cfg: JwtBearerOptions) =
  cfg.SaveToken <- true
  cfg.IncludeErrorDetails <- true

  cfg.Authority <-
    sprintf "https://cognito-idp.%s.amazonaws.com/%s/" Config.region Config.userPoolId

  cfg.Audience <- Config.clientId

  cfg.TokenValidationParameters <-
    TokenValidationParameters
      (ValidIssuer =
        sprintf "https://cognito-idp.%s.amazonaws.com/%s" Config.region Config.userPoolId)

  cfg.TokenValidationParameters.ValidateAudience <- false

let configureServices (services: IServiceCollection) =
  services.AddCors() |> ignore
  services.AddGiraffe() |> ignore

  services
    .AddAuthentication(authenticationOptions)
    .AddJwtBearer(Action<JwtBearerOptions> jwtBearerOptions)
  |> ignore

  services.AddAuthorization(Action<AuthorizationOptions> authorizationOptions)
  |> ignore


let configureLogging (builder: ILoggingBuilder) =
  builder.AddConsole().AddDebug() |> ignore

[<EntryPoint>]
let main args =
  let contentRoot = Directory.GetCurrentDirectory()
  let webRoot = Path.Combine(contentRoot, "WebRoot")

  Host
    .CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(fun webHostBuilder ->
      webHostBuilder
        .UseContentRoot(contentRoot)
        .UseWebRoot(webRoot)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
      |> ignore)
    .Build()
    .Run()

  0
