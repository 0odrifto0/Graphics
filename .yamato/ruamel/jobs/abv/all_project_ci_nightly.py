from ruamel.yaml.scalarstring import DoubleQuotedScalarString as dss
from ..utils.namer import *
from ..utils.yml_job import YMLJob


def get_job_definition(editor, projects, test_platforms, nightly_additions):  # only run for 2020.1 and trunk

    # define dependencies
    dependencies = [{
            'path': f'{abv_filepath()}#{abv_job_id_all_project_ci(editor["version"])}',
            'rerun': 'always'}]

    for test_platform in test_platforms: # TODO replace with all_smoke_tests if rerun strategy can override lower level ones
        dependencies.append({
            'path': f'{abv_filepath()}#{abv_job_id_smoke_test(editor["version"],test_platform["name"])}',
            'rerun': 'always'})

    for a in nightly_additions:
        for tp_name in a["test_platform_names"]:
            dependencies.append({
                'path': f'{project_filepath_specific(a["project_name"], a["platform_name"], a["api_name"])}#{project_job_id_test(a["project_name"], a["platform_name"], a["api_name"], tp_name, editor["version"])}',
                'rerun': 'always'})
        
    # construct job
    job = YMLJob()
    job.set_name(f'_Nightly ABV against { editor["version"] }')
    job.add_dependencies(dependencies)
    job.add_var_custom_revision(editor["version"])
    job.set_trigger_recurrent('master','0 * * ?')
    return job


class ABV_AllProjectCiNightlyJob():
    
    def __init__(self, editor, projects, test_platforms, nightly_config):
        if editor["version"] not in nightly_config["allowed_editors"]:
            raise Exception(f'Tried to construct nightly with PR trigger for version {editor["version"]}')
        self.job_id = abv_job_id_all_project_ci_nightly(editor["version"])
        self.yml = get_job_definition(editor, projects, test_platforms, nightly_config["additional_jobs"]).yml