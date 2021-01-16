import {
  OAuthScope,
  ResourceServerScope,
  UserPool,
} from "@aws-cdk/aws-cognito";
import * as cdk from "@aws-cdk/core";
import * as config from "./config.json";

export class CognitoStack extends cdk.Stack {
  constructor(scope: cdk.App, id: string, props?: cdk.StackProps) {
    super(scope, id, props);
    const pool = new UserPool(this, "Pool", {
      userPoolName: config.userpoolName,
    });

    const apiScope = new ResourceServerScope({
      scopeName: config.scopeName,
      scopeDescription: "API access",
    });

    const userServer = pool.addResourceServer("ResourceServer", {
      userPoolResourceServerName: config.resourceServerName,
      identifier: config.resourceServerIdentifier,
      scopes: [apiScope],
    });

    const client = pool.addClient("react-client", {
      userPoolClientName: config.clientName,
      generateSecret: false,
      oAuth: {
        callbackUrls: ["http://localhost:3000/callback"],
        logoutUrls: ["http://localhost:3000"],
        flows: {
          authorizationCodeGrant: true,
          clientCredentials: false,
          implicitCodeGrant: false,
        },
        scopes: [
          OAuthScope.EMAIL,
          OAuthScope.OPENID,
          OAuthScope.PROFILE,
          OAuthScope.resourceServer(userServer, apiScope),
        ],
      },
    });

    const domain = pool.addDomain("userpool-domain", {
      cognitoDomain: {
        domainPrefix: config.domainPrefix,
      },
    });

    new cdk.CfnOutput(this, "userpoolId", {
      value: pool.userPoolId,
    });
    new cdk.CfnOutput(this, "reactClientId", {
      value: client.userPoolClientId,
    });
    new cdk.CfnOutput(this, "domain", {
      value: domain.domainName,
    });
    new cdk.CfnOutput(this, "scopeName", {
      value: OAuthScope.resourceServer(userServer, apiScope).scopeName,
    });
  }
}
