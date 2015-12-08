using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;

namespace LanLordlAPIs.Models.Input_Models
{
    public class public_class_GameThriveMsgContent
    {
    }
    public class synapseOrderAPIInputClass
    {
        public string amount { get; set; }
        public string seller_id { get; set; }
        public string bank_id { get; set; }
        public string supp_id { get; set; }
        public string oauth_consumer_key { get; set; }

        public string note { get; set; }
        public string status_url { get; set; }
        public string facilitator_fee { get; set; }
    }

    public class SynapseV3AddTrans_ReusableClass
    {
        public bool success { get; set; }
        public string ErrorMessage { get; set; }
        public SynapseV3AddTrans_Resp responseFromSynapse { get; set; }

    }
    public class SynapseV3AddTrans_Resp
    {
        public bool success { get; set; }
        public SynapseV3AddTrans_Resp_trans trans { get; set; }
        public synapseV3Response_error error { get; set; }
    }
    public class SynapseV3AddTrans_Resp_trans
    {
        public SynapseV3AddTrans_Resp_trans_id _id { get; set; }
        public SynapseV3AddTrans_Resp_trans_amount amount { get; set; }

        public SynapseV3AddTrans_Resp_trans_client client { get; set; }
        public SynapseV3AddTrans_Resp_trans_extra extra { get; set; }
        public SynapseV3AddTrans_Resp_trans_fees[] fees { get; set; }
        public SynapseV3AddTrans_Resp_trans_from from { get; set; }
        public SynapseV3AddTrans_Resp_trans_recentstatus recent_status { get; set; }
        public SynapseV3AddTrans_Resp_trans_timeline[] timeline { get; set; }
        public SynapseV3AddTrans_Resp_trans_to to { get; set; }
    }
    public class synapseV3Response_error
    {
        public string en { get; set; }
    }
    public class SynapseV3AddTrans_Resp_trans_id
    {
        [JsonProperty(PropertyName = "$oid")]
        public string oid { get; set; }
    }
    public class SynapseV3AddTrans_Resp_trans_amount
    {
        public string amount { get; set; }
        public string currency { get; set; }
    }
    public class SynapseV3AddTrans_Resp_trans_client
    {

        public string id { get; set; }
        public string name { get; set; }
    }
    public class SynapseV3AddTrans_Resp_trans_extra
    {
        public string ip { get; set; }
        public string note { get; set; }
        public SynapseV3AddTrans_Resp_trans_extra_date process_on { get; set; }

        public SynapseV3AddTrans_Resp_trans_extra_date created_on { get; set; }
        public string supp_id { get; set; }
        public string webhook { get; set; }
    }
    public class SynapseV3AddTrans_Resp_trans_extra_date
    {

        public DateTime date { get; set; }
    }
    public class SynapseV3AddTrans_Resp_trans_fees
    {
        public string fee { get; set; }
        public string note { get; set; }
        public SynapseV3AddTrans_Resp_trans_fees_to to { get; set; }
    }
    public class SynapseV3AddTrans_Resp_trans_from
    {
        public SynapseNodeId id { get; set; }
        public string type { get; set; }

        public synapseV3_user_reusable_class user { get; set; }


    }
    public class SynapseNodeId
    {
        [JsonProperty(PropertyName = "$oid")]
        public string oid { get; set; }
    }
    public class SynapseV3AddTrans_Resp_trans_fees_to
    {
        public SynapseV3AddTrans_Resp_trans_fees_to_id id { get; set; }
    }
    public class SynapseV3AddTrans_Resp_trans_fees_to_id
    {
        public string oid { get; set; }
    }
    public class synapseV3_user_reusable_class
    {
        public synapseV3Result_user_id _id { get; set; }

        public string[] legal_names { get; set; }

    }
    public class synapseV3Result_user_id
    {

        [JsonProperty(PropertyName = "$oid")]
        public string id { get; set; }
    }

    public class SynapseV3AddTrans_Resp_trans_recentstatus
    {
        public SynapseV3AddTrans_Resp_trans_timeline_date date { get; set; }
        public string note { get; set; }
        public string status { get; set; }
        public string status_id { get; set; }
    }
    public class SynapseV3AddTrans_Resp_trans_timeline_date
    {

        public DateTime date { get; set; }
    }
    public class SynapseV3AddTrans_Resp_trans_timeline
    {
        public SynapseV3AddTrans_Resp_trans_timeline_date date { get; set; }
        public string note { get; set; }
        public string status { get; set; }
        public string status_id { get; set; }
    }
    public class SynapseV3AddTrans_Resp_trans_to
    {
        public SynapseNodeId id { get; set; }
        public string type { get; set; }
        public synapseV3_user_reusable_class user { get; set; }
    }




    public class synapseSearchUserResponse
    {
        public string error_code { get; set; }
        public string http_code { get; set; }
        public string errorMsg { get; set; }
        public int page { get; set; }
        public int page_count { get; set; }
        public bool success { get; set; }
        public int user_count { get; set; }
        public synapseSearchUserResponse_User[] users { get; set; }
    }

    public class synapseSearchUserResponse_User
    {
        public synapseSearchUserResponse_Id _id { get; set; }
        public synapseSearchUserResponse_Client client { get; set; }
        public object[] emails { get; set; }
        public string[] legal_names { get; set; }
        public synapseSearchUserResponse_Node[] nodes { get; set; }
        public object[] photos { get; set; }
    }

    public class synapseSearchUserResponse_Id
    {
        public string oid { get; set; }
    }

    public class synapseSearchUserResponse_Client
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class synapseSearchUserResponse_Node
    {
        public synapseSearchUserResponse_Id1 _id { get; set; }
        public string allowed { get; set; }
        public synapseSearchUserResponse_Info info { get; set; }
        public bool is_active { get; set; }
        public string type { get; set; }
    }
    public class synapseSearchUserResponse_Id1
    {
        [JsonProperty(PropertyName = "$oid")]
        public string oid { get; set; }
    }
    public class synapseSearchUserResponse_Info
    {
        public string nickname { get; set; }
    }

    public class synapseSearchUserInputClass
    {
        public synapseSearchUser_Client client { get; set; }
        public synapseSearchUser_Filter filter { get; set; }
    }
    public class synapseSearchUser_Client
    {
        public string client_id { get; set; }
        public string client_secret { get; set; }
    }

    public class synapseSearchUser_Filter
    {
        public int page { get; set; }
        public bool exact_match { get; set; }
        public string query { get; set; }
    }
    public class NodePermissionCheckResult
    {
        public bool IsPermissionfound { get; set; }
        public string PermissionType { get; set; }
    }


    /************************/
    /**  V3 - Add Transfer   /
    /************************/
    // INPUT CLASSES
    public class SynapseV3AddTransInput
    {
        public SynapseV3Input_login login { get; set; }
        public SynapseV3Input_user user { get; set; }
        public SynapseV3AddTransInput_trans trans { get; set; }
    }













    /**************************/
    /****  FOR SYNAPSE V3  ****/
    /**************************/
    // CLASSES THAT ARE USED IN MULTIPLE API CALLS

    // INPUT CLASSES (Used in multiple methods)
    public class SynapseV3Input_login
    {
        public string oauth_key { get; set; }
    }
    public class SynapseV3Input_user
    {
        public string fingerprint { get; set; }
    }


    public class SynapseV3AddTransInput_trans
    {
        public SynapseV3AddTransInput_trans_from from { get; set; }
        public SynapseV3AddTransInput_trans_to to { get; set; }
        public SynapseV3AddTransInput_trans_amount amount { get; set; }
        public SynapseV3AddTransInput_trans_extra extra { get; set; }
        public SynapseV3AddTransInput_trans_fees[] fees { get; set; }
    }
    public class SynapseV3AddTransInput_trans_from
    {
        public string type { get; set; }
        public string id { get; set; }
    }
    public class SynapseV3AddTransInput_trans_to
    {
        public string type { get; set; }
        public string id { get; set; }
    }

    public class SynapseV3AddTransInput_trans_amount
    {
        public double amount { get; set; }
        public string currency { get; set; }
    }

    public class SynapseV3AddTransInput_trans_extra
    {
        public string supp_id { get; set; }
        public string note { get; set; }
        public string webhook { get; set; }
        public int process_on { get; set; }
        public string ip { get; set; }
    }

    public class SynapseV3AddTransInput_trans_fees
    {
        public string fee { get; set; }
        public string note { get; set; }
        public SynapseV3AddTransInput_trans_fees_to to { get; set; }
    }
    public class SynapseV3AddTransInput_trans_fees_to
    {
        public string id { get; set; }
    }

    public class order_api_main
    {
        public string balance { get; set; }
        public bool balance_verified { get; set; }
        public bool success { get; set; }

        public order_api_order_internal order { get; set; }
    }
    public class order_api_order_internal
    {
        public int account_type { get; set; }
        public string amount { get; set; }
        public string date { get; set; }
        public string date_settled { get; set; }
        public string discount { get; set; }
        public string facilitator_fee { get; set; }
        public string fee { get; set; }
        public string id { get; set; }
        public bool is_buyer { get; set; }
        public string note { get; set; }
        public string resource_uri { get; set; }

        public order_api_bank_internal bank { get; set; }
        public order_api_seller_internal seller { get; set; }

        public string status { get; set; }
        public string status_url { get; set; }
        public string ticket_number { get; set; }
        public string tip { get; set; }
        public string total { get; set; }
    }
    public class order_api_bank_internal
    {
        public int id { get; set; }
        public string nickname { get; set; }
    }

    public class order_api_seller_internal
    {
        public bool accept_gratuity { get; set; }
        public bool has_avatar { get; set; }
        public string seller_avatar { get; set; }
        public string seller_fullname { get; set; }
        public int seller_id { get; set; }
    }
}