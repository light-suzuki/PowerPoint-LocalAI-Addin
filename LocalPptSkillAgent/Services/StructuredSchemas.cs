using Newtonsoft.Json.Linq;

namespace LocalPptSkillAgent.Services
{
    public static class StructuredSchemas
    {
        public static JObject ForSkill(string skill)
        {
            var schema = (JObject)LayoutActionPlan.DeepClone();
            var allowed = SkillActionPolicyService.GetPreferredActions(skill);
            var actionType = schema["properties"]?["actions"]?["items"]?["properties"]?["type"] as JObject;
            if (actionType != null && allowed.Count > 0) actionType["description"] = "Preferred for this skill: " + string.Join(", ", allowed);
            var issueType = schema["properties"]?["issues"]?["items"]?["properties"]?["severity"] as JObject;
            if (issueType != null) issueType["enum"] = new JArray("info", "warning", "error");
            return schema;
        }

        public static JObject SlideContentPlan(int? exactCount = null)
        {
            var slides = new JObject
            {
                ["type"] = "array",
                ["minItems"] = 1,
                ["maxItems"] = 12,
                ["items"] = new JObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JObject
                    {
                        ["slide_role"] = new JObject { ["type"] = "string", ["enum"] = new JArray("cover", "context", "question", "method", "evidence", "comparison", "interpretation", "model", "summary", "next_step") },
                        ["layout"] = new JObject { ["type"] = "string", ["enum"] = new JArray("title_only", "title_body", "title_two_column", "title_diagram", "title_diagram_two_column", "section_header", "summary", "method_flow", "result_with_figure", "hypothesis_model") },
                        ["composition"] = new JObject { ["type"] = "string", ["enum"] = new JArray("single_focus", "text_visual_split", "two_column", "three_step", "comparison", "evidence_plus_takeaway", "three_cards") },
                        ["visual_type"] = new JObject { ["type"] = "string", ["enum"] = new JArray("none", "native_diagram", "process", "comparison", "evidence_frame", "metric", "key_takeaway") },
                        ["visual_brief"] = new JObject { ["type"] = "string", ["maxLength"] = 300 },
                        ["title"] = new JObject { ["type"] = "string", ["maxLength"] = 80 },
                        ["message"] = new JObject { ["type"] = "string", ["maxLength"] = 240 },
                        ["body"] = new JObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 6, ["items"] = new JObject { ["type"] = "string", ["maxLength"] = 300 } },
                        ["notes"] = new JObject { ["type"] = "string", ["maxLength"] = 2000 },
                        ["citation_note"] = new JObject { ["type"] = "string", ["maxLength"] = 1000 }
                    },
                    ["required"] = new JArray("layout", "title", "message", "body", "notes", "citation_note")
                }
            };
            return new JObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JObject
                {
                    ["summary"] = new JObject { ["type"] = "string", ["maxLength"] = 500 },
                    ["slides"] = slides
                },
                ["required"] = new JArray("summary", "slides")
            };
        }

        public static JObject LayoutActionPlan => JObject.Parse(@"{
          'type':'object',
          'properties':{
            'skill':{'type':'string'},
            'target':{'type':'string'},
            'summary':{'type':'string'},
            'issues':{'type':'array','items':{'type':'object','properties':{
              'issue_type':{'type':'string'},'severity':{'type':'string'},'message':{'type':'string'},
              'slide':{'type':'integer'},'shape_ids':{'type':'array','items':{'type':'integer'}}
            },'required':['issue_type','severity','message','slide','shape_ids']}},
            'actions':{'type':'array','items':{
              'type':'object',
              'properties':{
                'type':{'type':'string'},'slide':{'type':'integer'},'shape_id':{'type':'integer'},
                'shape_ids':{'type':'array','items':{'type':'integer'}},
                'dx':{'type':'number'},'dy':{'type':'number'},'left':{'type':'number'},'top':{'type':'number'},
                'width':{'type':'number'},'height':{'type':'number'},'font_size':{'type':'number'},'text':{'type':'string'},
                'align_mode':{'type':'string'},'distribute_mode':{'type':'string'},'reason':{'type':'string'},
                'diagram':{'type':'object','properties':{
                  'diagram_type':{'type':'string'},'slide':{'type':'integer'},'placement':{'type':'string'},
                  'objects':{'type':'array','items':{'type':'object','properties':{
                    'id':{'type':'string'},'type':{'type':'string'},'text':{'type':'string'},
                    'x':{'type':'number'},'y':{'type':'number'},'w':{'type':'number'},'h':{'type':'number'},
                    'from':{'type':'string'},'to':{'type':'string'},'style_role':{'type':'string'}
                  },'required':['type']}}
                },'required':['diagram_type','slide','objects']},
                'slide_plan':{'type':'object','properties':{
                  'insert_after':{'type':'integer'},'insert_before':{'type':'integer'},'layout':{'type':'string'},
                  'title':{'type':'string'},'message':{'type':'string'},'body':{'type':'string'},
                  'notes':{'type':'string'},'citation_note':{'type':'string'},
                  'diagram':{'type':'object','properties':{
                    'diagram_type':{'type':'string'},'slide':{'type':'integer'},'placement':{'type':'string'},
                    'objects':{'type':'array','items':{'type':'object','properties':{
                      'id':{'type':'string'},'type':{'type':'string'},'text':{'type':'string'},
                      'x':{'type':'number'},'y':{'type':'number'},'w':{'type':'number'},'h':{'type':'number'},
                      'from':{'type':'string'},'to':{'type':'string'},'style_role':{'type':'string'}
                    },'required':['type']}}
                  },'required':['diagram_type','slide','objects']}
                },'required':['layout','title']}
              },
              'required':['type','slide']
            }}
          },
          'required':['skill','target','summary','issues','actions']
        }");

        public static JObject FactCheckResult => JObject.Parse(@"{
          'type':'object','properties':{
            'target':{'type':'string'},'summary':{'type':'string'},'external_evidence_used':{'type':'boolean'},
            'claims':{'type':'array','items':{'type':'object','properties':{
              'slide':{'type':'integer'},'shape_id':{'type':['integer','null']},'claim':{'type':'string'},
              'status':{'type':'string'},'reason':{'type':'string'},'evidence':{'type':'array','items':{'type':'string'}},
              'suggested_slide_text':{'type':['string','null']}
            },'required':['slide','claim','status','reason','evidence']}}
          },'required':['target','summary','claims']
        }");

        public static JObject FactCheckClaim => JObject.Parse(@"{
          'type':'object','properties':{
            'slide':{'type':'integer'},'shape_id':{'type':['integer','null']},'claim':{'type':'string'},
            'status':{'type':'string'},'reason':{'type':'string'},'evidence':{'type':'array','items':{'type':'string'}},
            'suggested_slide_text':{'type':['string','null']}
          },'required':['slide','claim','status','reason','evidence']
        }");
    }
}
