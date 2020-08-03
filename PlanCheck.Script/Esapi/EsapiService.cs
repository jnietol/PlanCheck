﻿using System.Linq;
using System.Threading.Tasks;
using EsapiEssentials.Plugin;
using VMS.TPS.Common.Model.API;
using System.IO;
using PlanCheck.Helpers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media.Media3D;
using System;

namespace PlanCheck
{
    public class EsapiService : EsapiServiceBase<PluginScriptContext>, IEsapiService
    {
        private readonly MetricCalculator _metricCalc;

        public EsapiService(PluginScriptContext context) : base(context)
        {
            _metricCalc = new MetricCalculator();
        }

        public Task<Plan[]> GetPlansAsync() =>
           RunAsync(context => context.Patient.Courses?
               .SelectMany(x => x.GetPlanSetupsAndSums())
               .Select(x => new Plan
               {
                   PlanId = x.Id,
                   CourseId = x.GetCourse().Id,
                   PlanType = x.GetType().ToString()
               })
               .ToArray());

        public Task<ObservableCollection<StructureViewModel>> GetStructuresAsync(string courseId, string planId) =>
            RunAsync(context =>
            {
                var planningItem = Extensions.GetPlanningItem(context.Patient, courseId, planId);
                var ss = planningItem?.StructureSet;
                var ssvm = StructureSetListViewModel.GetStructureList(ss);
                return ssvm;
            });

        public Task<string[]> GetBeamIdsAsync(string courseId, string planId) =>
            RunAsync(context =>
            {
                var planningItem = Extensions.GetPlanningItem(context.Patient, courseId, planId);
                var planSetup = (PlanSetup)planningItem;          
                return planSetup.Beams.Where(x => x.IsSetupField != true).Select(x => x.Id).ToArray() ?? new string[0];
            });

        public Task<Point3D> GetCameraPositionAsync(string courseId, string planId, string beamId) =>
            RunAsync(context =>
            {
                var planningItem = Extensions.GetPlanningItem(context.Patient, courseId, planId);
                var plan = (PlanSetup)planningItem;
                var beam = plan.Beams.FirstOrDefault(x => x.Id == beamId);
                return CollisionSummariesCalculator.GetCameraPosition(beam);
            });

        public Task<Point3D> GetIsocenterAsync(string courseId, string planId, string beamId) =>
            RunAsync(context =>
            {
                var planningItem = Extensions.GetPlanningItem(context.Patient, courseId, planId);
                var plan = (PlanSetup)planningItem;
                var beam = plan.Beams.FirstOrDefault(x => x.Id == beamId);
                return CollisionSummariesCalculator.GetIsocenter(beam);
            });

        public Task<ObservableCollection<ErrorViewModel>> GetErrorsAsync(string courseId, string planId) =>
            RunAsync(context =>
            {
                var planningItem = Extensions.GetPlanningItem(context.Patient, courseId, planId);
                var calculator = new ErrorCalculator();
                var errorGrid = calculator.Calculate(planningItem);
                return errorGrid;
            });

        public Task<Tuple<CollisionCheckViewModel, Model3DGroup>> GetBeamCollisionsAsync(string courseId, string planId, string beamId) =>
            RunAsync(context =>
            {
                var planningItem = Extensions.GetPlanningItem(context.Patient, courseId, planId);
                var calculator = new CollisionSummariesCalculator();
                var plan = (PlanSetup)planningItem;
                var beam = plan.Beams.FirstOrDefault(x => x.Id == beamId);
                var tuple = calculator.CalculateBeamCollision(plan, beam);
                return tuple;
            });

        public Task<Model3DGroup> AddCouchBodyAsync(string courseId, string planId) =>
            RunAsync(context =>
            {
                var planningItem = Extensions.GetPlanningItem(context.Patient, courseId, planId);
                var structureSet = planningItem.StructureSet;
                var body = structureSet.Structures.Where(x => x.Id.Contains("BODY")).First();
                Structure couch = null;
                try
                {
                    couch = structureSet.Structures.Where(x => x.Id.Contains("CouchSurface")).First();
                }
                catch
                {

                }

                
                //var calculator = new CollisionSummariesCalculator();
                return CollisionSummariesCalculator.AddCouchBodyMesh(body, couch);
            });
        public Task<Model3DGroup> AddFieldMeshAsync(Model3DGroup modelGroup, string courseId, string planId, string beamId, string status) =>
            RunAsync(context =>
            {
                var planningItem = Extensions.GetPlanningItem(context.Patient, courseId, planId);
                //var calculator = new CollisionSummariesCalculator();
                var plan = (PlanSetup)planningItem;
                var beam = plan.Beams.FirstOrDefault(x => x.Id == beamId);
                return CollisionSummariesCalculator.AddFieldMesh(plan, beam, status);
            });

        public Task<PQMViewModel[]> GetObjectivesAsync(ConstraintViewModel constraint) =>
            RunAsync(context =>
            {
                var objectives = Objectives.GetObjectives(constraint);
                return objectives.ToArray() ?? new PQMViewModel[0];
            });

        public Task<string> CalculateMetricDoseAsync(string courseId, string planId, string structureId, string templateId, string dvhObjective, string goal, string variation) =>
            RunAsync(context => CalculateMetricDose(context.Patient, courseId, planId, structureId, templateId, dvhObjective, goal, variation));

        public string CalculateMetricDose(Patient patient, string courseId, string planId, string structureId, string templateId, string dvhObjective, string goal, string variation)
        {
            var plan = Extensions.GetPlanningItem(patient, courseId, planId);
            var planVM = new PlanningItemViewModel(plan);
            var structure = Extensions.GetStructure(plan, structureId);

            DirectoryInfo constraintDir = new DirectoryInfo(Path.Combine(AssemblyHelper.GetAssemblyDirectory(), "ConstraintTemplates"));
            string firstFileName = constraintDir.GetFiles().FirstOrDefault().ToString();
            string firstConstraintFilePath = Path.Combine(constraintDir.ToString(), firstFileName);

            // make sure the workbook template exists
            if (!System.IO.File.Exists(firstConstraintFilePath))
            {
                System.Windows.MessageBox.Show(string.Format("The template file '{0}' chosen does not exist.", firstConstraintFilePath));
            }
            var structureVM = new StructureViewModel(structure);
            string metric = "";
            //var goal = "";
            string result = "";
            //string variation = "";
                if (templateId == structureId)
                {
                    metric = dvhObjective;
                    //goal = objective.Goal;
                    //variation = objective.Variation;
                    result = _metricCalc.CalculateMetric(planVM.PlanningItemStructureSet, structureVM, planVM, metric);
                }                 
                else
                    result = "";                
            return result;
        }

        public string EvaluateMetricDose(string result, string goal, string variation)
        {
            var met = "";
            met = _metricCalc.EvaluateMetric(result, goal, variation);
            return met;
        }

        public Task<string> EvaluateMetricDoseAsync(string result, string goal, string variation) =>
            RunAsync(context => EvaluateMetricDose(result, goal, variation));
    }
}