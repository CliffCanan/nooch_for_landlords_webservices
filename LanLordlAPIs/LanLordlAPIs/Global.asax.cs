using LanLordlAPIs.Classes.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using LanLordlAPIs.Models.db_Model;

namespace LanLordlAPIs
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            log4net.Config.XmlConfigurator.Configure();
        }


        private void runAllDailyChecks(object a)
        {
            try
            {
                while (true)
                {
                    //Sleep for 1 second
                    Thread.Sleep(1000);
                    //Run this at 02:00PM (~10:00am EST) each day
                    if (DateTime.Now.Hour == 17 && DateTime.Now.Minute == 0 && DateTime.Now.Second == 0 &&
                        (((Convert.ToInt16(DateTime.Now.DayOfWeek) + 6) % 7) < 5))  // Only run Mon - Fri
                    {
                        Logger.Info("DAILY SCAN -> runAllDailyChecks");

                        // NoochChecks(); -- not enables yet
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("DAILY SCAN -> runAllDailyChecks FAILED - [Exception: " + ex + "]");
            }
            finally
            {
            }
        }


        private bool NoochChecks()
        {
            using (NOOCHEntities obj = new NOOCHEntities())
            {
                // 1. Get All property Units and check if any of these is occupied by any Tenant
                var allPropertyUnits =
                    (from c in obj.PropertyUnits where c.IsDeleted == false && c.IsOccupied == true select c).ToList();

                if (allPropertyUnits.Count > 0)
                {
                    // iterating through each property unit
                    DateTime todaysDate = DateTime.Now.Date;

                    foreach (PropertyUnit propUnit in allPropertyUnits)
                    {
                        // checking if current property unit is occupied?

                        var propOccupyTest =
                            (from c in obj.UnitsOccupiedByTenants
                             where c.UnitId == propUnit.UnitId && c.IsDeleted == false
                             select c).FirstOrDefault();
                        if (propOccupyTest != null)
                        {
                            // yes property occupied... checking if rent paid for this month
                            if (propOccupyTest.IsPaymentDueForThisMonth == true || propOccupyTest.IsPaymentDueForThisMonth == null)
                            {

                                // These both needs to discussed with Cliff...
                                // We must have rent start date for each property unit...if landlord didn't provide this..then we will configure when landlord adds tenant to unit
                                DateTime propRentStartDate = Convert.ToDateTime(propUnit.RentStartDate).Date;

                                //Lease length will let us know for how long we will keep charging tenant for the property... we need to make sure landlord always enters int value like 1,2,6,12 ....this number indicates no. of months
                                DateTime propLeaseLengthStartDate = Convert.ToDateTime(todaysDate.AddMonths(Convert.ToInt16(propUnit))).Date;

                                // need 


                                // checking due date for this month
                                string dueDateInDB = propUnit.DueDate ?? "";
                                if (String.IsNullOrEmpty(dueDateInDB))
                                {
                                    if (dueDateInDB == "1st of Month")
                                    {
                                        // 1st of every month
                                        DateTime firstDateOfCurrentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                                        if ((firstDateOfCurrentMonth == todaysDate) && (firstDateOfCurrentMonth >= propRentStartDate) && (firstDateOfCurrentMonth <= propLeaseLengthStartDate))
                                        {
                                            // day to make transaction...pay rent
                                            //AutoPayRentForGivenTenantAndUnit();
                                        }
                                    }
                                    if (dueDateInDB == "Last of Month")
                                    {
                                        // last day of every month
                                        DateTime firstDateOfCurrentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                                        DateTime lastDateOfCurrentMonth = firstDateOfCurrentMonth.AddMonths(1).AddDays(-1);
                                        if ((lastDateOfCurrentMonth == todaysDate) && (lastDateOfCurrentMonth >= propRentStartDate) && (lastDateOfCurrentMonth <= propLeaseLengthStartDate))
                                        {
                                            // day to make transaction...pay rent
                                            //AutoPayRentForGivenTenantAndUnit();
                                        }
                                    }

                                    if (dueDateInDB == "15th of Month")
                                    {
                                        // 15th of every month
                                        DateTime fiftinthDateOfCurrentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 15);


                                        if ((fiftinthDateOfCurrentMonth == todaysDate) && (fiftinthDateOfCurrentMonth >= propRentStartDate) && (fiftinthDateOfCurrentMonth <= propLeaseLengthStartDate))
                                        {
                                            // day to make transaction...pay rent
                                            //AutoPayRentForGivenTenantAndUnit();
                                        }
                                    }

                                    if (dueDateInDB.Length > 0)
                                    {
                                        // custom date of month
                                        DateTime customDateOfMonth = Convert.ToDateTime(dueDateInDB);


                                        if ((customDateOfMonth == todaysDate) && (customDateOfMonth >= propRentStartDate) && (customDateOfMonth <= propLeaseLengthStartDate))
                                        //if (customDateOfMonth.Date == todaysDate)
                                        {
                                            // day to make transaction...pay rent
                                            //AutoPayRentForGivenTenantAndUnit();
                                        }
                                    }
                                }


                            }
                        }


                    }

                }
            }
            return true;
        }
        //method to be written yet
        private void AutoPayRentForGivenTenantAndUnit()
        {

        }
       
    }
}
