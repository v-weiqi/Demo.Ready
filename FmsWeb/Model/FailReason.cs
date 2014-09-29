using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FmsWeb.Model
{
    public enum FailReason
    {
        App_Runtime_failed_on_Azure=1,
        App_Runtime_failed_on_Katal=2,
        App_Runtime_failed_on_IIS_7=3,
        App_Runtime_failed_on_IIS_75=4,
        App_Runtime_failed_on_IIS_8=5,
        App_Runtime_failed_on_IIS_Express=6,
        App_Runtime_failed_on_IIS_6=7,
        Invalid_Package=8,
        Publish_to_Remote_server_Failed=9,
        Publish_to_Azure_site_Failed=10,
        Publish_to_Katal_Site_Failed=11,
        Download_from_Remote_server_Failed=12,
        Download_from_Azure_site_Failed=13,
        Download_to_form_Site_Failed=14,
        App_detection_Failed_in_Web_Matrix=15,
    }
}