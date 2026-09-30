RAVEN Architecture Diagram
┌─────────────────────────────────────────────────────────────┐
│                         RAVEN UI                            │
│                                                             │
│  Web Dashboard | VS Extension | VS Code Extension | Teams  │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│                        RAVEN CORE                           │
│                                                             │
│  Context Engine  | Workflow Engine | Agent Coordinator      │
│  Memory Engine   | Security Model  | User Preferences       │
└───────┬───────────┬───────────┬───────────┬────────────────┘
        │           │           │           │
        ▼           ▼           ▼           ▼

┌──────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
│ RAVEN Relay │ │RAVEN Planner│ │RAVEN Scout │ │RAVEN Insight│
└──────┬───────┘ └──────┬──────┘ └──────┬──────┘ └──────┬──────┘
       │                │               │                │
       │                │               │                │
       ▼                ▼               ▼                ▼

 ┌───────────┐     ┌──────────┐    ┌──────────┐     ┌──────────┐
 │   Rally   │     │  Story   │    │  Source  │     │ Metrics  │
 │    API    │     │ Analysis │    │ Analysis │     │ Forecast │
 └───────────┘     └──────────┘    └──────────┘     └──────────┘

                          │
                          ▼

┌─────────────────────────────────────────────────────────────┐
│                    RAVEN DEVELOPER HUB                      │
└───────────────┬───────────────────────┬─────────────────────┘
                │                       │
                ▼                       ▼

     ┌──────────────────┐    ┌──────────────────┐
     │ RAVEN Developer  │    │ RAVEN Reviewer   │
     │ (Implementation) │    │ (Quality Gate)   │
     └────────┬─────────┘    └────────┬─────────┘
              │                       │
              ▼                       ▼

      ┌─────────────┐         ┌──────────────┐
      │ GitLab Duo  │         │ Security     │
      │   Agent     │         │ Compliance   │
      └──────┬──────┘         └──────┬───────┘
             │                       │
             └───────────┬───────────┘
                         ▼

                 ┌───────────────┐
                 │ RAVEN Forge   │
                 │ Git / MR Hub  │
                 └───────┬───────┘
                         │
                         ▼

                 ┌───────────────┐
                 │ GitLab        │
                 │ Repository    │
                 │ Merge Request │
                 └───────┬───────┘
                         │
                         ▼

              ┌─────────────────────┐
              │ RAVEN Scribe        │
              │ Documentation Agent │
              └─────────┬───────────┘
                        │
                        ▼

                  ┌────────────┐
                  │ M365       │
                  │ Copilot    │
                  └─────┬──────┘
                        │
                        ▼

              ┌─────────────────────┐
              │ RAVEN Reporter      │
              │ Standups / Reports  │
              └─────────┬───────────┘
                        │
                        ▼

                    Rally
                  Management
                  Stakeholders


